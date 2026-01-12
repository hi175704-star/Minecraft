using System.Collections;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class MinecraftEditor : EditorWindow
{
    // 카테고리 목록
    private string[] categories = { "Prefab Palette", "Grid Snap", "Undo/Redo", "Auto Save", "Scene Version Backup" };
    private int selectedCategory = 0;

    // Prefab Palette 기능을 위한 변수
    private string searchQuery = "";
    private GameObject[] prefabs;
    private GameObject selectedPrefab;
    private Vector2 scrollPos;

    // Grid Snap 기능을 위한 변수
    private bool useGridSnap = true;
    private float gridSize = 1f;

    // Auto Save 기능을 위한 변수
    private bool autoSaveEnabled = false;
    private float autoSaveInterval = 60f;
    private double lastAutoSaveTime;

    // DrawSceneBackup 기능을 위한 변수
    private bool autoBackupEnabled = false;
    private float backupInterval = 300f; // 5분
    private double lastBackupTime = 0;
    private string backupFolderPath = "Assets/SceneBackups";

    [MenuItem("Dev/Minecraft Editor")]
    public static void ShowWindow()
    {
        GetWindow<MinecraftEditor>("Minecraft Editor");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();  // 가로로 UI를 배치하는 함수

        // 왼쪽 사이드바 (카테고리 리스트)
        DrawSidebar();

        // 오른쪽 콘텐츠 영역
        DrawContent();

        EditorGUILayout.EndHorizontal(); // 가로로 UI를 배치하는 함수 끝
    }

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200)); // 왼쪽 패널 너비 고정

        GUILayout.Space(5);
        GUILayout.Label("Categories", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 카테고리 버튼들 (세로로 배치)
        for (int i = 0; i < categories.Length; i++)
        {
            //삼항연산자로 선택한 버튼과 아닌 버튼의 스타일을 다르게 설정
            GUIStyle style = (i == selectedCategory)
                ? new GUIStyle(EditorStyles.helpBox) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }
                : new GUIStyle("button") { alignment = TextAnchor.MiddleLeft };

            if (GUILayout.Button(categories[i], style, GUILayout.Height(25)))
                selectedCategory = i;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawContent()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.Space(10);

        switch (selectedCategory)
        {
            case 0:
                DrawPrefabPalette();
                break;
            case 1:
                DrawGridSnap();
                break;
            case 2:
                DrawUndoRedo();
                break;
            case 3:
                DrawAutoSave();
                break;
            case 4:
                DrawSceneBackup();
                break;
        }

        EditorGUILayout.EndVertical();
    }

    // 각 카테고리별 섹션 ---------------------------

    private void DrawPrefabPalette() // 프리팹 목록과 배치 기능
    {
        GUILayout.Label("Prefab Palette Settings", EditorStyles.boldLabel);

        // 상단 검색창
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Search:", GUILayout.Width(50));
        string newSearch = GUILayout.TextField(searchQuery, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
        if (newSearch != searchQuery)
        {
            searchQuery = newSearch;
        }

        if (GUILayout.Button("↻", EditorStyles.toolbarButton, GUILayout.Width(30)))
            LoadPrefabs();

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 프리팹 목록
        if (prefabs == null || prefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("프리팹이 없습니다.", MessageType.Warning);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var prefab in prefabs)
        {
            if (!string.IsNullOrEmpty(searchQuery) &&
                !prefab.name.ToLower().Contains(searchQuery.ToLower()))
                continue;

            //삼항연산자로 선택한 버튼과 아닌 버튼의 스타일을 다르게 설정
            GUIStyle style = (prefab == selectedPrefab)
                ? new GUIStyle(EditorStyles.helpBox) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }
                : new GUIStyle("button");

            if (GUILayout.Button(prefab.name, style, GUILayout.Height(22)))
            {
                selectedPrefab = prefab;
                Repaint();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        LoadPrefabs();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void LoadPrefabs() // Prefabs 폴더에 있는 모든 프리팹을 불러온다
    {
        prefabs = Resources.LoadAll<GameObject>("Prefabs");
    }

    private void OnSceneGUI(SceneView sceneView) // Prefab Plette, GridSnap, NavMesh Auto Bake 기능 관리
    {
        if (selectedPrefab == null)
            return;

        Event e = Event.current;

        // 왼쪽 클릭으로 프리팹 배치
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 스냅 적용 여부 확인
                if (useGridSnap)
                {
                    float gs = gridSize;
                    Vector3 p = hit.point;
                    p.x = Mathf.Round(p.x / gs) * gs;
                    p.y = Mathf.Round(p.y);// / gs) * gs;
                    p.z = Mathf.Round(p.z / gs) * gs;
                    hit.point = p;
                }


                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                Undo.RegisterCreatedObjectUndo(instance, "Place Prefab"); 

                // y값을 보정하기 전에 클릭한 위치로 옮기기
                instance.transform.position = hit.point;

                // 프리팹의 경계 가져오기
                float yOffset = 0;

                Collider col = instance.GetComponent<Collider>();
                if (col != null)
                {
                    yOffset = col.bounds.extents.y; // 콜라이더 높이의 절반
                }

                else
                {
                    Renderer rend = instance.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        yOffset = rend.bounds.extents.y;
                    }
                }
                instance.transform.position += Vector3.up * yOffset;
                //instance.transform.position = hit.point; // 피봇이 바닥에 맞춰졌을때 사용
                //instance.transform.position = hit.point + Vector3.up *0.5f; // 고정 높이로 올리기

                //Selection.activeGameObject = instance; // V키 버텍스 스냅을 사용하기 위해 사용

                //// NavMesh Auto Bake 기능
                //if (autoBakeEnabled)
                //    BakeNavMeshNow();

                e.Use();
            }
        }
        // ESC로 선택 해제
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            selectedPrefab = null;
            Repaint();
        }


    }

    private void DrawGridSnap() // Grid Snap UI
    {
        GUILayout.Label("Grid Snap Settings", EditorStyles.boldLabel);
        useGridSnap = EditorGUILayout.Toggle("Enable Grid Snap", useGridSnap);
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
    }

    private void DrawUndoRedo() // Undo/Redo UI
    {
        GUILayout.Label("Undo / Redo Settings", EditorStyles.boldLabel);

        if (GUILayout.Button("Undo", GUILayout.Height(25)))
            Undo.PerformUndo(); // Undo 스택에서 한단계 되돌리는 기능

        if (GUILayout.Button("Redo", GUILayout.Height(25)))
            Undo.PerformRedo(); // Undo로 되돌린 작업을 다시 앞으로 되돌리는 기능

        EditorGUILayout.HelpBox("Ctrl+Z, Ctrl+Y로도 작동합니다.", MessageType.Info);
    }

    private void DrawAutoSave() // Auto Save UI
    {
        GUILayout.Label("Auto Save Settings", EditorStyles.boldLabel);

        // 자동 저장 켜기/끄기 토글
        autoSaveEnabled = EditorGUILayout.Toggle("Enable Auto Save", autoSaveEnabled);

        // 간격 설정 (초 단위)
        autoSaveInterval = EditorGUILayout.FloatField("Save Interval (sec)", autoSaveInterval);

        // 수동 저장 버튼
        if (GUILayout.Button("Save Now", GUILayout.Height(25)))
            SaveScene();

        EditorGUILayout.HelpBox("지정한 간격마다 자동으로 씬을 저장합니다.", MessageType.Info);
    }

    private void SaveScene() // 현재 씬을 저장하는 기능
    {
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[AutoSave] Scene saved at " + System.DateTime.Now);
    }

    private void DrawSceneBackup() // SceneBackup UI
    {
        GUILayout.Label("Scene Version Backup Settings", EditorStyles.boldLabel);

        autoBackupEnabled = EditorGUILayout.Toggle("Enable Auto Backup", autoBackupEnabled);
        backupInterval = EditorGUILayout.FloatField("Backup Interval (sec)", backupInterval);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Backup Folder:", GUILayout.Width(100));
        backupFolderPath = EditorGUILayout.TextField(backupFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Backup Folder", backupFolderPath, "");
            if (!string.IsNullOrEmpty(selected))
                backupFolderPath = selected;
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Backup Now", GUILayout.Height(25)))
            BackupCurrentScene();

        EditorGUILayout.HelpBox("설정된 주기마다 자동으로 씬을 백업하고, 이전 버전은 날짜별로 관리됩니다.", MessageType.Info);

        // 자동 백업 업데이트 호출
        AutoBackupUpdate();
    }

    private void BackupCurrentScene() // 파일을 복제하여 씬을 저장하는 기능
    {
        string scenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogWarning("현재 씬이 저장되지 않았습니다. 먼저 저장해주세요.");
            return;
        }

        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupName = $"{sceneName}_backup_{timestamp}.unity";

        string fullBackupPath = System.IO.Path.Combine(backupFolderPath, backupName);
        System.IO.Directory.CreateDirectory(backupFolderPath);

        AssetDatabase.CopyAsset(scenePath, fullBackupPath);
        AssetDatabase.Refresh();

        Debug.Log($"[Scene Backup] {sceneName} 백업 완료 : {fullBackupPath}");
    }

    private void AutoBackupUpdate()
    {
        if (!autoBackupEnabled)
            return;

        // 씬을 자동으로 백업
        if (EditorApplication.timeSinceStartup - lastBackupTime > backupInterval)
        {
            lastBackupTime = EditorApplication.timeSinceStartup;
            BackupCurrentScene();
        }
    }

    private void Update()
    {
        // 씬을 자동으로 저장
        if (autoSaveEnabled && EditorApplication.timeSinceStartup - lastAutoSaveTime > autoSaveInterval)
        {
            lastAutoSaveTime = EditorApplication.timeSinceStartup;
            SaveScene();
        }
    }


}