﻿using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PixelWizards.MultiScene
{
    /// <summary>
    /// Simple wrapper that the timeline track talks to.
    /// 
    /// Note: implements the scene loading separately from the MultiSceneConfig 
    /// so we can do callbacks on scene load (coroutines etc) that we can't do in a scriptable objects
    /// </summary>
    public class MultiSceneSwapHelper : MonoBehaviour
    {
        public class SceneModel
        {
            public bool isLevelLoading = false;
            public string levelLoading = String.Empty;
        }

        private SceneModel model = new SceneModel();
        public MultiSceneLoader multiSceneConfig;

        /// <summary>
        /// Load a given config from our MultiSceneConfig by name, optionally unloading all existing scenes and optionally using Async loading 
        /// </summary>
        /// <param name="configName"></param>
        /// <param name="unloadExisting"></param>
        /// <param name="useAsyncLoading"></param>
        public void LoadConfig( string configName, bool unloadExisting, bool useAsyncLoading)
        {
            LoadSceneConfigByName(configName, unloadExisting, useAsyncLoading);
        }

        public void LoadConfig( string configName, bool unloadExisting)
        {
            LoadSceneConfigByName(configName, unloadExisting, false);
        }

        public void UnloadConfig( string configName)
        {
            UnloadConfigInternal(configName);
        }

        /// <summary>
        /// Loads a scene config by name
        /// </summary>
        /// <param name="configName"></param>
        /// <param name="unloadExisting"></param>
        private void LoadSceneConfigByName(string configName, bool unloadExisting, bool useAsyncLoading)
        {
            foreach (var entry in multiSceneConfig.config)
            {
                if (entry.name == configName)
                {
                    LoadSceneConfig(entry, unloadExisting, useAsyncLoading);
                    return;
                }
            }
            // if we get here, we didn't find the config
            Debug.LogWarning("MultiSceneLoader::LoadSceneConfigByName() - could not find config: " + configName);
        }


        /// <summary>
        /// Load a specific scene config, optionally unloads existing first (ie can be optionally additively loaded)
        /// </summary>
        /// <param name="config"></param>
        /// <param name="unloadExisting"></param>
        private void LoadSceneConfig(SceneConfig config, bool unloadExisting, bool useAsyncLoading)
        {
            for (int i = 0; i < config.sceneList.Count; i++)
            {
                var sceneName = config.sceneList[i].name;

                if (Application.isPlaying)
                {
                    if (SceneManager.GetSceneByName(sceneName) == null)
                        Debug.LogError("Scene: " + sceneName + " doesn't exist in build settings");

                    if (!IsScene_CurrentlyLoaded(sceneName))
                    {
                        if (i == 0)
                        {
                            if (unloadExisting )
                            {
                                // if we need to unload existing, then load the first in single mode, otherwise everything is additive
                                LoadNewScene(sceneName, useAsyncLoading, callback =>
                                {
                                    if (IsScene_CurrentlyLoaded(sceneName))
                                    {
                      //                  Debug.Log("Update light probes");

                                        // if so, then do light magic
                                        LightProbes.TetrahedralizeAsync();
                                    }
                                });
                            }
                            else
                            {
                                // and the rest additive
                                LoadSceneAdditive(sceneName, useAsyncLoading, callback =>
                                {
                                    if (IsScene_CurrentlyLoaded(sceneName))
                                    {
                             //           Debug.Log("Update light probes");

                                        // if so, then do light magic
                                        LightProbes.TetrahedralizeAsync();
                                    }
                                });
                            }

                        }
                        else
                        {
                            // and the rest additive
                            LoadSceneAdditive(sceneName, useAsyncLoading, callback =>
                            {
                                if (IsScene_CurrentlyLoaded(sceneName))
                                {
                         //           Debug.Log("Update light probes");

                                    // if so, then do light magic
                                    LightProbes.TetrahedralizeAsync();
                                }
                            });
                        }
                    }
                }
                else
                {
#if UNITY_EDITOR
                    // if it's not already loaded
                    if (!IsScene_CurrentlyLoaded_inEditor(sceneName))
                    {
                        // load the scene
                        EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(config.sceneList[i]), OpenSceneMode.Additive);

                        // now is it loaded?
                        if (IsScene_CurrentlyLoaded_inEditor(sceneName))
                        {
                        //    Debug.Log("Update light probes");
                            // if so, then do light magic
                            LightProbes.Tetrahedralize();
                        }
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Unloads any scenes within the specified config
        /// </summary>
        /// <param name="thisConfig"></param>
        private void UnloadConfigInternal(string thisConfig)
        {
            foreach (var entry in multiSceneConfig.config)
            {
                if (entry.name == thisConfig)
                {
                    UnloadConfigInternal(entry);
                }
            }
        }

        /// <summary>
        /// Unloads any scene from a specific config ( if they are loaded )
        /// </summary>
        /// <param name="thisConfig"></param>
        private void UnloadConfigInternal(SceneConfig thisConfig)
        {
            var loadedSceneCount = SceneManager.sceneCount;
            for (var i = 0; i < loadedSceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                foreach (var scene in thisConfig.sceneList)
                {
                    if (loadedScene.name == scene.name)
                    {
                        if (Application.isPlaying)
                        {
                            if (IsScene_CurrentlyLoaded(loadedScene.name))
                            {
                                if (loadedScene.isLoaded)
                                {
                                    SceneManager.UnloadSceneAsync(loadedScene);
                                }
                            }
                        }
#if UNITY_EDITOR
                        else
                        {
                            EditorSceneManager.CloseScene(loadedScene, true);
                        }
#endif        
                    }
                }
            }
        }

        private void LoadNewScene(string newEnvironment, bool useAsyncLoading, Action<string> callback = null)
        {
            model.levelLoading = newEnvironment;

            StartCoroutine(LoadSceneInternal(model.levelLoading, false, useAsyncLoading, callback));
        }

        /// <summary>
        /// Load a new Level additively. Fires a callback with the name of the newly loaded level once the load is completed.
        /// </summary>
        private void LoadSceneAdditive(string newEnvironment, bool useAsyncLoading, Action<string> callback = null)
        {
            model.levelLoading = newEnvironment;

            StartCoroutine(LoadSceneInternal(model.levelLoading, true, useAsyncLoading, callback));
        }

        /// <summary>
        /// Set the specified scene to be the active one
        /// </summary>
        /// <param name="thisScene"></param>
        private void SetActiveScene(string thisScene)
        {
            var activeScene = SceneManager.GetSceneByName(thisScene);
            SceneManager.SetActiveScene(activeScene);
        }

        private void UnloadScene(string sceneName)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }

        /// <summary>
        /// Async level loading using the new Unity 5 Scene Management API
        /// </summary>
        private IEnumerator LoadSceneInternal(string newScene, bool useAdditive, bool useAsyncLoading, Action<string> callback = null)
        {
            AsyncOperation async = new AsyncOperation();
            try
            {
                var sceneMode = LoadSceneMode.Single;
                if (useAdditive)
                    sceneMode = LoadSceneMode.Additive;

                if( useAsyncLoading)
                {
                    async = SceneManager.LoadSceneAsync(newScene, sceneMode);
                    async.allowSceneActivation = true;          // scenes will activate themselves
                }
                else
                {
                    SceneManager.LoadScene(newScene, sceneMode);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Caught Exception " + e.Message + " while loading scene: " + newScene + " - might not be in your build settings?");
                async = null;
            }

            if (async != null)
            {
                while (!async.isDone)
                {
                    model.isLevelLoading = true;
                    yield return null;
                }

                if (async.isDone)
                {
                    model.isLevelLoading = false;
                    // TODO: should add a timer so we can log how long level loads take

                    callback?.Invoke(newScene);

                    model.levelLoading = string.Empty;
                }
            }
            else
            {
                Debug.LogError("Async loading of scene failed for scene: " + newScene + " - might not be in your build settings?");
            }


            yield break;
        }


        /// <summary>
        /// Check if a scene is loaded at edit time
        /// </summary>
        /// <param name="thisScene"></param>
        /// <returns></returns>
#if UNITY_EDITOR
        private bool IsScene_CurrentlyLoaded_inEditor(string thisScene)
        {
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; ++i)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);

                if (scene.name == thisScene)
                {
                   // Debug.Log("Editor: Scene already loaded");
                    return true; //the scene is already loaded
                }
            }

         //   Debug.Log("Editor: Scene not loaded");
            //scene not currently loaded in the hierarchy:
            return false;
        }
#endif

        /// <summary>
        /// Check if a scene is loaded at runtime
        /// </summary>
        /// <param name="thisScene"></param>
        /// <returns></returns>
        private bool IsScene_CurrentlyLoaded(string thisScene)
        {
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name == thisScene)
                {
                    //the scene is already loaded
                    return true;
                }
            }
            return false;   //scene not currently loaded in the hierarchy
        }

    }
}