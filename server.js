// server.js
const https = require("https");
const fs = require("fs");
const path = require("path");
const http = require("http");
const express = require("express");
const mysql = require("mysql2");
const cors = require("cors");

const app = express();
app.use(cors());
app.use(express.json());

// MySQL 연결 설정
const db = mysql.createConnection({
    host: "127.0.0.1",
    port: 3306,
    user: "daebeom",
    password: "1234",      // MySQL root 비밀번호
    database: "minecraftdb",        // 새 DB 이름
});

// DB 연결
db.connect((err) => {
    if (err) {
        console.error("❌ DB 연결 실패:", err);
        return;
    }
    console.log("✅ MySQL 연결 성공!");
});

// 기본 라우트 테스트
app.get("/", (req, res) => {
    res.send("서버가 정상적으로 실행되고 있습니다!");
});

// 아이템 추가
app.get("/inventory/:playerId", (req, res) => {
    const playerId = req.params.playerId;
    const query = "SELECT * FROM player_inventory WHERE player_id = ?";
    db.query(query, [playerId], (err, results) => {
        if (err) return res.status(500).json({ error: "DB 조회 실패" });
        // MySQL에서 NULL로 온 durability는 null로 내려감
        res.json(results);
    });
});

// POST /inventory/add (insert / update / delete)
// POST /inventory/add (insert / update / delete)
app.post("/inventory/add", (req, res) => {
    console.log("POST /inventory/add 들어옴:", req.body);
    const { player_id, item_name, count, durability, slot_index } = req.body;
    console.log("slot_index 들어오는 값:", slot_index);

    // --- 유효성 검사 ---
    if (player_id == null || item_name == null || slot_index == null) {
        console.warn("⚠️ 필수 데이터 누락:", req.body);
        return res.status(400).json({ error: "필수 데이터 누락" });
    }

    const countValue = parseInt(count);
    const durabilityValue = (durability != null && parseInt(durability) >= 0) ? parseInt(durability) : null;

    // --- 삭제 처리 (count <= 0) ---
    if (countValue <= 0) {
        const deleteQuery = "DELETE FROM player_inventory WHERE player_id = ? AND slot_index = ?";
        db.query(deleteQuery, [player_id, slot_index], (err, result) => {
            if (err) {
                console.error("❌ DB 삭제 실패:", err);
                return res.status(500).json({ error: "DB 삭제 실패" });
            }
            console.log("🗑️ 아이템 삭제 완료:", result.affectedRows);
            return res.json({ message: "삭제 완료", affectedRows: result.affectedRows });
        });
        return;
    }

    // --- INSERT ON DUPLICATE KEY UPDATE ---
    const query = `
        INSERT INTO player_inventory (player_id, item_name, count, durability, slot_index)
        VALUES (?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
            item_name = VALUES(item_name),
            count = count + VALUES(count),
            durability = VALUES(durability)
    `;

    db.query(query, [player_id, item_name, countValue, durabilityValue, slot_index], (err, result) => {
        if (err) {
            console.error("❌ DB 삽입/갱신 실패:", err);
            return res.status(500).json({ error: "DB 삽입/갱신 실패" });
        }

        if (result.affectedRows === 1) {
            console.log("🆕 새 아이템 추가 완료, ID:", result.insertId);
            return res.json({ message: "새 아이템 추가 완료", id: result.insertId });
        } else {
            console.log("✅ 수량 누적 갱신 완료");
            return res.json({ message: "수량 누적 완료" });
        }
    });
});

// POST /inventory/use
app.post("/inventory/use", (req, res) => {
    console.log("POST /inventory/use 들어옴:", req.body);

    const { player_id, item_name, use_count, durability, slot_index } = req.body;

    // 필수 데이터 체크
    if (player_id == null || item_name == null || use_count == null || slot_index == null) {
        console.warn("⚠️ 필수 데이터 누락:", req.body);
        return res.status(400).json({ error: "필수 데이터 누락" });
    }

    const useCountValue = parseInt(use_count);
    const durabilityValue = (durability != null && parseInt(durability) >= 0) ? parseInt(durability) : null;

    // DB에서 해당 슬롯 아이템 조회
    const query = "SELECT * FROM player_inventory WHERE player_id = ? AND slot_index = ?";
    db.query(query, [player_id, slot_index], (err, results) => {
        if (err) {
            console.error("❌ DB 조회 실패:", err);
            return res.status(500).json({ error: "DB 조회 실패" });
        }

        if (results.length === 0) {
            console.warn("⚠️ 아이템 없음 (slot_index 기준):", player_id, slot_index);
            return res.status(404).json({ error: "아이템 없음" });
        }

        const current = results[0];
        if (current.item_name !== item_name) {
            console.warn("⚠️ slot_index와 item_name 불일치:", current.item_name, item_name);
            return res.status(400).json({ error: "slot_index와 item_name 불일치" });
        }

        let newCount = current.count - useCountValue;

        if (newCount <= 0) {
            // 아이템 삭제
            const deleteQuery = "DELETE FROM player_inventory WHERE player_id = ? AND slot_index = ?";
            db.query(deleteQuery, [player_id, slot_index], (err2, r2) => {
                if (err2) {
                    console.error("❌ DB 삭제 실패:", err2);
                    return res.status(500).json({ error: "DB 삭제 실패" });
                }
                console.log(`🗑️ ${item_name} 삭제 완료 (use)`);
                return res.json({ message: "아이템 사용 후 삭제 완료" });
            });
        } else {
            // 아이템 수량 및 내구도 업데이트
            const updateQuery = "UPDATE player_inventory SET count = ?, durability = ? WHERE player_id = ? AND slot_index = ?";
            db.query(updateQuery, [newCount, durabilityValue, player_id, slot_index], (err3, r3) => {
                if (err3) {
                    console.error("❌ DB 갱신 실패:", err3);
                    return res.status(500).json({ error: "DB 갱신 실패" });
                }
                console.log(`✅ ${item_name} 사용 완료, count=${newCount}, durability=${durabilityValue}`);
                return res.json({ message: "아이템 사용 완료", newCount, durability: durabilityValue });
            });
        }
    });
});


app.post("/inventory/drop", (req, res) => {
    console.log("POST /inventory/drop 들어옴:", req.body);
    const { player_id, item_name } = req.body;

    if (!player_id || !item_name) {
        return res.status(400).json({ error: "필수 데이터 누락" });
    }

    const deleteQuery = `
        DELETE FROM player_inventory
        WHERE player_id = ? AND item_name = ?
        LIMIT 1
    `;

    db.query(deleteQuery, [player_id, item_name], (err, result) => {
        if (err) {
            console.error("❌ DB 삭제 실패:", err);
            return res.status(500).json({ error: "DB 삭제 실패" });
        }

        if (result.affectedRows === 0) {
            console.warn("⚠️ 삭제 대상 없음:", player_id, item_name);
            return res.status(404).json({ error: "삭제 대상 없음" });
        }

        console.log(`🗑️ 아이템 버리기 완료: ${item_name}`);
        return res.json({ message: "아이템 버리기 완료" });
    });
});

app.post("/inventory/move", async (req, res) => {
    console.log("[MOVE REQ]", req.body);

    const { player_id, fromSlot, toSlot, item_name, count, remainingCount } = req.body;

    if (player_id == null || fromSlot == null || count == null) {
        return res.status(400).json({ error: "필수 데이터 누락" });
    }

    try {
        // 1) fromSlot 아이템 조회
        const [fromRows] = await db.promise().query(
            "SELECT * FROM player_inventory WHERE player_id = ? AND slot_index = ?",
            [player_id, fromSlot]
        );
        const fromItem = fromRows[0];

        if (!fromItem) {
            // fromSlot에 원래 아이템 없으면 종료
            return res.json({ success: true });
        }

        const isSwap = remainingCount === -1;

        // 2) fromSlot 업데이트 (스왑이 아니고 remainingCount >= 0)
        if (!isSwap && fromSlot !== toSlot) { if (remainingCount <= 0) { await db.promise().query( "DELETE FROM player_inventory WHERE player_id = ? AND slot_index = ?", [player_id, fromSlot] ); } else { await db.promise().query( "UPDATE player_inventory SET count = ? WHERE player_id = ? AND slot_index = ?", [remainingCount, player_id, fromSlot] ); } }
        // 3) 단순 집기/분할이면 여기서 종료
        if (fromSlot === toSlot) {
            return res.json({ success: true });
        }

        // 4) toSlot 조회
        const [toRows] = await db.promise().query(
            "SELECT * FROM player_inventory WHERE player_id = ? AND slot_index = ?",
            [player_id, toSlot]
        );
        const toItem = toRows[0];

        // 5) toSlot 비어있으면 INSERT
        if (!toItem) {
            await db.promise().query(
                "INSERT INTO player_inventory (player_id, item_name, slot_index, count) VALUES (?, ?, ?, ?)",
                [player_id, item_name, toSlot, count]
            );
            return res.json({ success: true });
        }

        // 6) 같은 아이템이면 병합
        if (toItem.item_name === item_name) {
            await db.promise().query(
                "UPDATE player_inventory SET count = ? WHERE id = ?",
                [toItem.count + count, toItem.id]
            );
            return res.json({ success: true });
        }

        // 7) 다른 아이템이면 스왑
        await db.promise().query(
            "UPDATE player_inventory SET slot_index = ? WHERE id = ?",
            [fromSlot, toItem.id]
        );
        await db.promise().query(
            "UPDATE player_inventory SET slot_index = ? WHERE id = ?",
            [toSlot, fromItem.id]
        );

        return res.json({ success: true });

    } catch (err) {
        console.error("MOVE ERROR:", err);
        return res.status(500).json({ error: "서버 오류" });
    }
});


const options = {
  key: fs.readFileSync("/etc/letsencrypt/live/minehub.co.kr/privkey.pem"),
  cert: fs.readFileSync("/etc/letsencrypt/live/minehub.co.kr/fullchain.pem"),
};

// 서버 실행
https.createServer(options, app).listen(443, () => {
  console.log("HTTPS 서버가 443 포트에서 실행 중");
});

//http.createServer((req, res) => {
 // res.writeHead(301, { "Location": "https://" + req.headers["host"] + req.url });
 // res.end();
//}).listen(80, () => {
  //console.log("➡️ HTTP 요청은 HTTPS로 리다이렉트 중");
//});