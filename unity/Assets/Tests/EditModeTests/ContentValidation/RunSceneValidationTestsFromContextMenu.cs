﻿using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Tests.Utilities.Constants;
using static Tests.Utilities.UtilityFunctions;

namespace Tests.EditModeTests.ContentValidation
{
    /// <summary>
    /// Adds a "Run Tests" entry to the hierarchy window's scene context menu.
    /// On click starts all scene specific content validation tests and shows the results in a small popup.
    /// </summary>
    [InitializeOnLoad]
    public class RunSceneValidationTestsFromContextMenu : ScriptableObject, ICallbacks 
    {
        static RunSceneValidationTestsFromContextMenu()
        {
            // Pass scene as context
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += (menu, scene) =>
            {
                // Only add to context menu for experiment scenes
                if (scene.path.Contains("experiments"))
                    menu.AddItem(new GUIContent(GuiDropDownText), false, DoRunTests, scene);
            };
        }

        private static void DoRunTests(object userData)
        {
            CreateInstance<RunSceneValidationTestsFromContextMenu>()
                .StartTestRun((Scene) userData);
        }

        private static TestRunnerApi _runnerApi;
        private static TestRunnerApi RunnerApi =>
            _runnerApi ? _runnerApi : (_runnerApi = CreateInstance<TestRunnerApi>());
        
        private readonly Regex _experimentNameRegex = new Regex($@"\w+\.({TypePC}|{TypeVR})");

        private void StartTestRun(Scene scene)
        {
            Debug.Log($"Starting test run for scene {scene.path}");
            
            // Not unloaded by Resources.UnloadUnusedAssets, will be destroyed in RunFinished callback
            hideFlags = HideFlags.HideAndDontSave;
            
            // Test results don't show up after re-compile when no Test Runner window is opened - force refresh
            if (!EditorWindow.HasOpenInstances<TestRunnerWindow>())
            {
                EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
                if (EditorWindow.HasOpenInstances<TestRunnerWindow>())
                    EditorWindow.GetWindow<TestRunnerWindow>().Close();
            }

            // filter by test group name
            var scenePath = scene.path;
            var experimentName = _experimentNameRegex.Match(scenePath).ToString();
            var sceneTestsGroupName = $"Tests\\.ContentValidation\\.(Pc|Vr)SceneValidationTests\\(\"{experimentName}\",.*";

            RunnerApi.Execute(new ExecutionSettings
            {
                filters = new []
                {
                    new Filter()
                    {
                        groupNames = new[] { sceneTestsGroupName },
                        testMode = TestMode.EditMode
                    }
                }
            });
        }

        public void OnEnable()
        {
            RunnerApi.RegisterCallbacks(this);
        }

        public void OnDisable()
        {
            RunnerApi.UnregisterCallbacks(this);
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result) { }

        /// <summary>
        /// Display test results as a small popup window with confirmation button
        /// </summary>
        public void RunFinished(ITestResultAdaptor result)
        {
            // No tests found
            if (!result.HasChildren)
            {
                EditorUtility.DisplayDialog(GuiPopupTitle, GuiNoTestsFound, GuiConfirm);
            }
            // No failed tests: all passed or skipped
            else if (result.FailCount == 0)
            {
                EditorUtility.DisplayDialog(GuiPopupTitle,
                    result.SkipCount > 0
                        ? $"{result.PassCount} test{(result.PassCount > 1 ? "s" : "")} passed ({result.SkipCount} skipped)."
                        : $"All {result.PassCount} test{(result.PassCount > 1 ? "s" : "")} passed.", "Ok");
            }
            // 1 or more tests failed
            else
            {
                ReportTestFailureWithPopup(result);
                EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            }

            DestroyImmediate(this);
        }
    }
}
