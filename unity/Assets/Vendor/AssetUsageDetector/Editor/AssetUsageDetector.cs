// Asset Usage Detector - by Suleyman Yasir KULA (yasirkula@gmail.com)

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;
using System.Text;
using Object = UnityEngine.Object;
#if UNITY_2018_3_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#endif

namespace AssetUsageDetectorNamespace
{
	[Flags]
	public enum SceneSearchMode { None = 0, OpenScenes = 1, ScenesInBuildSettingsAll = 2, ScenesInBuildSettingsTickedOnly = 4, AllScenes = 8 };
	public enum PathDrawingMode { Full = 0, ShortRelevantParts = 1, Shortest = 2 };

	public partial class AssetUsageDetector
	{
		#region Helper Classes
		[Serializable]
		public class Parameters
		{
			public Object[] objectsToSearch = null;

			public SceneSearchMode searchInScenes = SceneSearchMode.AllScenes;
			public Object[] searchInScenesSubset = null;
			public Object[] excludedScenesFromSearch = null;
			public bool searchInAssetsFolder = true;
			public Object[] searchInAssetsSubset = null;
			public Object[] excludedAssetsFromSearch = null;
			public bool dontSearchInSourceAssets = true;
			public bool searchInProjectSettings = true;

			public int searchDepthLimit = 4;
			public BindingFlags fieldModifiers = BindingFlags.Public | BindingFlags.NonPublic;
			public BindingFlags propertyModifiers = BindingFlags.Public | BindingFlags.NonPublic;
			public bool searchNonSerializableVariables = true;

			public bool lazySceneSearch = false;
			public bool noAssetDatabaseChanges = false;
			public bool showDetailedProgressBar = false;
		}
		#endregion

		// A set that contains the searched asset(s) and their sub-assets (if any)
		private readonly HashSet<Object> objectsToSearchSet = new HashSet<Object>();
		// Scenes of scene object(s) in objectsToSearchSet
		private readonly HashSet<string> sceneObjectsToSearchScenesSet = new HashSet<string>();
		// Project asset(s) in objectsToSearchSet
		private readonly HashSet<Object> assetsToSearchSet = new HashSet<Object>();
		// assetsToSearchSet's path(s)
		private readonly HashSet<string> assetsToSearchPathsSet = new HashSet<string>();
		// The root prefab objects in assetsToSearchSet that will be used to search for prefab references
		private readonly List<GameObject> assetsToSearchRootPrefabs = new List<GameObject>( 4 );
		// Path(s) of the assets that should be excluded from the search
		private readonly HashSet<string> excludedAssetsPathsSet = new HashSet<string>();
		// Extension(s) of assets that will always be searched in detail
		private readonly HashSet<string> alwaysSearchedExtensionsSet = new HashSet<string>();

		// Results for the currently searched scene
		private SearchResultGroup currentSearchResultGroup;

		// An optimization to search an object only once (key is a hash of the searched object)
		private readonly Dictionary<string, ReferenceNode> searchedObjects = new Dictionary<string, ReferenceNode>( 32768 );

		// Stack of SearchObject function parameters to avoid infinite loops (which happens when same object is passed as parameter to function)
		private readonly List<object> callStack = new List<object>( 64 );

		private int searchDepthLimit; // Depth limit for recursively searching variables of objects

		private Object currentSearchedObject;
		private int currentDepth;

		private bool dontSearchInSourceAssets;
		private bool searchingSourceAssets;
		private bool isInPlayMode;

#if UNITY_2018_3_OR_NEWER
		private UnityEditor.SceneManagement.PrefabStage openPrefabStage;
		private GameObject openPrefabStagePrefabAsset;
#if UNITY_2020_1_OR_NEWER
		private GameObject openPrefabStageContextObject;
#endif
#endif

		private int searchedObjectsCount; // Number of searched objects
		private double searchStartTime;

		private readonly List<ReferenceNode> nodesPool = new List<ReferenceNode>( 32 );

		// Search for references!
		public SearchResult Run( Parameters searchParameters )
		{
			if( searchParameters == null )
			{
				Debug.LogError( "searchParameters must not be null!" );
				return new SearchResult( false, null, null, this, searchParameters );
			}

			if( searchParameters.objectsToSearch == null )
			{
				Debug.LogError( "objectsToSearch list is empty!" );
				return new SearchResult( false, null, null, this, searchParameters );
			}

			if( !EditorApplication.isPlaying && !Utilities.AreScenesSaved() )
			{
				// Don't start the search if at least one scene is currently dirty (not saved)
				Debug.LogError( "Save open scenes first!" );
				return new SearchResult( false, null, null, this, searchParameters );
			}

			List<SearchResultGroup> searchResult = null;

			isInPlayMode = EditorApplication.isPlaying;
#if UNITY_2018_3_OR_NEWER
			openPrefabStagePrefabAsset = null;
			string openPrefabStageAssetPath = null;
			openPrefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if( openPrefabStage != null )
			{
				if( !openPrefabStage.stageHandle.IsValid() )
					openPrefabStage = null;
				else
				{
					if( openPrefabStage.scene.isDirty )
					{
						// Don't start the search if a prefab stage is currently open and dirty (not saved)
						Debug.LogError( "Save open prefabs first!" );
						return new SearchResult( false, null, null, this, searchParameters );
					}

#if UNITY_2020_1_OR_NEWER
					string prefabAssetPath = openPrefabStage.assetPath;
#else
					string prefabAssetPath = openPrefabStage.prefabAssetPath;
#endif
					openPrefabStagePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>( prefabAssetPath );
					openPrefabStageAssetPath = prefabAssetPath;

#if UNITY_2020_1_OR_NEWER
					openPrefabStageContextObject = openPrefabStage.openedFromInstanceRoot;
#endif
				}
			}
#endif

			// Get the scenes that are open right now
			SceneSetup[] initialSceneSetup = !isInPlayMode ? EditorSceneManager.GetSceneManagerSetup() : null;

			// Make sure that the AssetDatabase is up-to-date
			AssetDatabase.SaveAssets();

			try
			{
				// Initialize commonly used variables
				searchResult = new List<SearchResultGroup>(); // Overall search results

				currentSearchedObject = null;
				currentDepth = 0;
				searchedObjectsCount = 0;
				searchStartTime = EditorApplication.timeSinceStartup;

				searchDepthLimit = searchParameters.searchDepthLimit;
				dontSearchInSourceAssets = searchParameters.dontSearchInSourceAssets;

				searchedObjects.Clear();
				animationClipUniqueBindings.Clear();
				callStack.Clear();
				objectsToSearchSet.Clear();
				sceneObjectsToSearchScenesSet.Clear();
				assetsToSearchSet.Clear();
				assetsToSearchPathsSet.Clear();
				assetsToSearchRootPrefabs.Clear();
				excludedAssetsPathsSet.Clear();
				alwaysSearchedExtensionsSet.Clear();
				shaderIncludesToSearchSet.Clear();
#if UNITY_2017_3_OR_NEWER
				assemblyDefinitionFilesToSearch.Clear();
#endif

				if( assetDependencyCache == null )
				{
					LoadCache();
					searchStartTime = EditorApplication.timeSinceStartup;
				}
				else if( !searchParameters.noAssetDatabaseChanges )
				{
					foreach( var cacheEntry in assetDependencyCache.Values )
						cacheEntry.verified = false;
				}

				foreach( var cacheEntry in assetDependencyCache.Values )
					cacheEntry.searchResult = CacheEntry.Result.Unknown;

				lastRefreshedCacheEntry = null;

				// Store the searched objects(s) in HashSets
				HashSet<string> folderContentsSet = new HashSet<string>();
				foreach( Object obj in searchParameters.objectsToSearch )
				{
					if( obj == null || obj.Equals( null ) )
						continue;

					if( obj.IsFolder() )
						folderContentsSet.UnionWith( Utilities.EnumerateFolderContents( obj ) );
					else
						AddSearchedObjectToFilteredSets( obj, true );
				}

				foreach( string filePath in folderContentsSet )
				{
					// Skip scene assets
					if( filePath.EndsWithFast( ".unity" ) )
						continue;

					Object[] assets = AssetDatabase.LoadAllAssetsAtPath( filePath );
					if( assets == null || assets.Length == 0 )
						continue;

					for( int i = 0; i < assets.Length; i++ )
						AddSearchedObjectToFilteredSets( assets[i], true );
				}

				// Find Project Settings to search for references. Don't search Project Settings if searched object(s) are all scene objects
				// as Project Settings can't hold references to scene objects
				string[] projectSettingsToSearch = new string[0];
				if( searchParameters.searchInProjectSettings && assetsToSearchSet.Count > 0 )
				{
					string[] projectSettingsAssets = Directory.GetFiles( "ProjectSettings" );
					projectSettingsToSearch = new string[projectSettingsAssets.Length];
					for( int i = 0; i < projectSettingsAssets.Length; i++ )
						projectSettingsToSearch[i] = "ProjectSettings/" + Path.GetFileName( projectSettingsAssets[i] );

					// AssetDatabase.GetDependencies doesn't work with Project Settings assets. By adding these assets to assetsToSearchPathsSet,
					// we make sure that AssetHasAnyReference returns true for these assets and they don't get excluded from the search
					assetsToSearchPathsSet.UnionWith( projectSettingsToSearch );
				}

				// Find the scenes to search for references
				HashSet<string> scenesToSearch = new HashSet<string>();
				if( searchParameters.searchInScenesSubset != null && searchParameters.searchInScenesSubset.Length > 0 )
				{
					foreach( Object obj in searchParameters.searchInScenesSubset )
					{
						if( obj == null || obj.Equals( null ) )
							continue;

						if( !obj.IsAsset() )
							continue;

						if( obj.IsFolder() )
						{
							string[] folderContents = AssetDatabase.FindAssets( "t:SceneAsset", new string[] { AssetDatabase.GetAssetPath( obj ) } );
							if( folderContents == null )
								continue;

							for( int i = 0; i < folderContents.Length; i++ )
								scenesToSearch.Add( AssetDatabase.GUIDToAssetPath( folderContents[i] ) );
						}
						else if( obj is SceneAsset )
							scenesToSearch.Add( AssetDatabase.GetAssetPath( obj ) );
					}
				}
				else if( ( searchParameters.searchInScenes & SceneSearchMode.AllScenes ) == SceneSearchMode.AllScenes )
				{
					// Get all scenes from the Assets folder
					string[] sceneGuids = AssetDatabase.FindAssets( "t:SceneAsset" );
					for( int i = 0; i < sceneGuids.Length; i++ )
						scenesToSearch.Add( AssetDatabase.GUIDToAssetPath( sceneGuids[i] ) );
				}
				else
				{
					if( ( searchParameters.searchInScenes & SceneSearchMode.OpenScenes ) == SceneSearchMode.OpenScenes )
					{
						// Get all open (and loaded) scenes
						for( int i = 0; i < SceneManager.loadedSceneCount; i++ )
						{
							Scene scene = EditorSceneManager.GetSceneAt( i );
							if( scene.IsValid() )
								scenesToSearch.Add( scene.path );
						}
					}

					bool searchInScenesInBuildTickedAll = ( searchParameters.searchInScenes & SceneSearchMode.ScenesInBuildSettingsAll ) == SceneSearchMode.ScenesInBuildSettingsAll;
					if( searchInScenesInBuildTickedAll || ( searchParameters.searchInScenes & SceneSearchMode.ScenesInBuildSettingsTickedOnly ) == SceneSearchMode.ScenesInBuildSettingsTickedOnly )
					{
						// Get all scenes in build settings
						EditorBuildSettingsScene[] scenesTemp = EditorBuildSettings.scenes;
						for( int i = 0; i < scenesTemp.Length; i++ )
						{
							if( ( searchInScenesInBuildTickedAll || scenesTemp[i].enabled ) )
								scenesToSearch.Add( scenesTemp[i].path );
						}
					}
				}

				// In Play mode, only open scenes can be searched
				if( isInPlayMode )
				{
					HashSet<string> openScenes = new HashSet<string>();
					for( int i = 0; i < SceneManager.loadedSceneCount; i++ )
					{
						Scene scene = EditorSceneManager.GetSceneAt( i );
						if( scene.IsValid() )
							openScenes.Add( scene.path );
					}

					scenesToSearch.RemoveWhere( ( path ) =>
					{
						if( !openScenes.Contains( path ) )
						{
							Debug.Log( "Can't search unloaded scenes while in play mode, skipped " + path );
							return true;
						}

						return false;
					} );
				}

				// Initialize data used by search functions
				InitializeSearchFunctionsData( searchParameters );

				// Initialize the nodes of searched asset(s)
				foreach( Object obj in objectsToSearchSet )
					searchedObjects.Add( obj.Hash(), PopReferenceNode( obj ) );

				// Progressbar values
				int searchProgress = 0;
				int searchTotalProgress = scenesToSearch.Count;
				if( isInPlayMode && searchParameters.searchInScenes != SceneSearchMode.None )
					searchTotalProgress++; // DontDestroyOnLoad scene

				if( searchParameters.showDetailedProgressBar )
					searchTotalProgress += projectSettingsToSearch.Length;

				// Don't search assets if searched object(s) are all scene objects as assets can't hold references to scene objects
				if( searchParameters.searchInAssetsFolder && assetsToSearchSet.Count > 0 )
				{
					currentSearchResultGroup = new SearchResultGroup( "Project Window (Assets)", SearchResultGroup.GroupType.Assets );

					// Get the paths of all assets that are to be searched
					IEnumerable<string> assetPaths;
					if( searchParameters.searchInAssetsSubset == null || searchParameters.searchInAssetsSubset.Length == 0 )
					{
						string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
						assetPaths = allAssetPaths;

						if( searchParameters.showDetailedProgressBar )
							searchTotalProgress += allAssetPaths.Length;
					}
					else
					{
						folderContentsSet.Clear();

						foreach( Object obj in searchParameters.searchInAssetsSubset )
						{
							if( obj == null || obj.Equals( null ) )
								continue;

							if( !obj.IsAsset() )
								continue;

							if( obj.IsFolder() )
								folderContentsSet.UnionWith( Utilities.EnumerateFolderContents( obj ) );
							else
								folderContentsSet.Add( AssetDatabase.GetAssetPath( obj ) );
						}

						assetPaths = folderContentsSet;

						if( searchParameters.showDetailedProgressBar )
							searchTotalProgress += folderContentsSet.Count;
					}

					// Calculate the path(s) of the assets that won't be searched for references
					if( searchParameters.excludedAssetsFromSearch != null )
					{
						foreach( Object obj in searchParameters.excludedAssetsFromSearch )
						{
							if( obj == null || obj.Equals( null ) )
								continue;

							if( !obj.IsAsset() )
								continue;

							if( obj.IsFolder() )
								excludedAssetsPathsSet.UnionWith( Utilities.EnumerateFolderContents( obj ) );
							else
								excludedAssetsPathsSet.Add( AssetDatabase.GetAssetPath( obj ) );
						}
					}

					if( EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching assets", 0f ) )
						throw new Exception( "Search aborted" );

					foreach( string path in assetPaths )
					{
						if( searchParameters.showDetailedProgressBar && ++searchProgress % 30 == 1 && EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching assets", (float) searchProgress / searchTotalProgress ) )
							throw new Exception( "Search aborted" );

						if( excludedAssetsPathsSet.Contains( path ) )
							continue;

						// If asset resides inside the Assets directory and is not a scene asset
						if( path.StartsWithFast( "Assets/" ) && !path.EndsWithFast( ".unity" ) )
						{
							if( !AssetHasAnyReference( path ) )
								continue;

							Object[] assets = AssetDatabase.LoadAllAssetsAtPath( path );
							if( assets == null || assets.Length == 0 )
								continue;

							for( int i = 0; i < assets.Length; i++ )
							{
								// Components are already searched while searching the GameObject
								if( assets[i] is Component )
									continue;

								BeginSearchObject( assets[i] );
							}
						}
					}

					// If a reference is found in the Project view, save the results
					if( currentSearchResultGroup.NumberOfReferences > 0 )
						searchResult.Add( currentSearchResultGroup );
				}

				// Search all assets inside the ProjectSettings folder
				if( projectSettingsToSearch.Length > 0 )
				{
					currentSearchResultGroup = new SearchResultGroup( "Project Settings", SearchResultGroup.GroupType.ProjectSettings );

					if( EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching Project Settings", (float) searchProgress / searchTotalProgress ) )
						throw new Exception( "Search aborted" );

					for( int i = 0; i < projectSettingsToSearch.Length; i++ )
					{
						if( searchParameters.showDetailedProgressBar && ++searchProgress % 30 == 1 && EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching Project Settings", (float) searchProgress / searchTotalProgress ) )
							throw new Exception( "Search aborted" );

						Object[] assets = AssetDatabase.LoadAllAssetsAtPath( projectSettingsToSearch[i] );
						if( assets != null && assets.Length > 0 )
						{
							for( int j = 0; j < assets.Length; j++ )
								BeginSearchObject( assets[j] );
						}
					}

					if( currentSearchResultGroup.NumberOfReferences > 0 )
						searchResult.Add( currentSearchResultGroup );
				}

				// Search non-serializable variables for references while searching a scene in play mode
				if( isInPlayMode )
					searchSerializableVariablesOnly = false;

				if( scenesToSearch.Count > 0 )
				{
					// Calculate the path(s) of the scenes that won't be searched for references
					HashSet<string> excludedScenesPathsSet = new HashSet<string>();
					if( searchParameters.excludedScenesFromSearch != null )
					{
						foreach( Object obj in searchParameters.excludedScenesFromSearch )
						{
							if( obj == null || obj.Equals( null ) )
								continue;

							if( !obj.IsAsset() )
								continue;

							if( obj.IsFolder() )
							{
								string[] folderContents = AssetDatabase.FindAssets( "t:SceneAsset", new string[] { AssetDatabase.GetAssetPath( obj ) } );
								if( folderContents == null )
									continue;

								for( int i = 0; i < folderContents.Length; i++ )
									excludedScenesPathsSet.Add( AssetDatabase.GUIDToAssetPath( folderContents[i] ) );
							}
							else if( obj is SceneAsset )
								excludedScenesPathsSet.Add( AssetDatabase.GetAssetPath( obj ) );
						}
					}

					foreach( string scenePath in scenesToSearch )
					{
						if( EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching scene: " + scenePath, (float) ++searchProgress / searchTotalProgress ) )
							throw new Exception( "Search aborted" );

						// Search scene for references
						if( string.IsNullOrEmpty( scenePath ) )
							continue;

						if( excludedScenesPathsSet.Contains( scenePath ) )
							continue;

						SearchScene( scenePath, searchResult, searchParameters.lazySceneSearch, initialSceneSetup );
					}
				}

				// Search through all the GameObjects under the DontDestroyOnLoad scene (if exists)
				if( isInPlayMode && searchParameters.searchInScenes != SceneSearchMode.None )
				{
					if( EditorUtility.DisplayCancelableProgressBar( "Please wait...", "Searching scene: DontDestroyOnLoad", 1f ) )
						throw new Exception( "Search aborted" );

					currentSearchResultGroup = new SearchResultGroup( "DontDestroyOnLoad", SearchResultGroup.GroupType.DontDestroyOnLoad );

					GameObject[] rootGameObjects = GetDontDestroyOnLoadObjects();
					for( int i = 0; i < rootGameObjects.Length; i++ )
						SearchGameObjectRecursively( rootGameObjects[i] );

					if( currentSearchResultGroup.NumberOfReferences > 0 )
						searchResult.Add( currentSearchResultGroup );
				}

				// Searching source assets last prevents some references from being excluded due to callStack.ContainsFast
				if( !dontSearchInSourceAssets )
				{
					searchingSourceAssets = true;

					foreach( Object obj in objectsToSearchSet )
					{
						currentSearchedObject = obj;
						SearchObject( obj );
					}

					searchingSourceAssets = false;
				}

				InitializeSearchResultNodes( searchResult );

				// Log some c00l stuff to console
				Debug.Log( "Searched " + searchedObjectsCount + " objects in " + ( EditorApplication.timeSinceStartup - searchStartTime ).ToString( "F2" ) + " seconds" );

				return new SearchResult( true, searchResult, initialSceneSetup, this, searchParameters );
			}
			catch( Exception e )
			{
				StringBuilder sb = new StringBuilder( objectsToSearchSet.Count * 50 + callStack.Count * 50 + 1000 );
				sb.AppendLine( "<b>AssetUsageDetector Error:</b>" ).AppendLine().Append( e ).AppendLine();

				Object latestUnityObjectInCallStack = AppendCallStackToStringBuilder( sb );

				sb.AppendLine( "Searching references of: " );
				foreach( Object obj in objectsToSearchSet )
				{
					if( obj )
						sb.Append( obj.name ).Append( " (" ).Append( obj.GetType() ).AppendLine( ")" );
				}

				Debug.LogError( sb.ToString(), latestUnityObjectInCallStack );

				try
				{
					InitializeSearchResultNodes( searchResult );
				}
				catch
				{ }

				return new SearchResult( false, searchResult, initialSceneSetup, this, searchParameters );
			}
			finally
			{
				currentSearchResultGroup = null;
				currentSearchedObject = null;

				EditorUtility.ClearProgressBar();

#if UNITY_2018_3_OR_NEWER
				// If a prefab stage was open when the search was triggered, try reopening the prefab stage after the search is completed
				if( !string.IsNullOrEmpty( openPrefabStageAssetPath ) )
				{
#if UNITY_2020_1_OR_NEWER
					bool shouldOpenPrefabStageWithoutContext = true;
					if( openPrefabStageContextObject != null && !openPrefabStageContextObject.Equals( null ) )
					{
						try
						{
							// Try to access this method: https://github.com/Unity-Technologies/UnityCsReference/blob/73925b1711847c067e607ec8371f8e9ffe7ab65d/Editor/Mono/SceneManagement/StageManager/PrefabStage/PrefabStageUtility.cs#L61-L65
							MethodInfo prefabStageOpenerWithContext = typeof( UnityEditor.SceneManagement.PrefabStageUtility ).GetMethod( "OpenPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[2] { typeof( string ), typeof( GameObject ) }, null );
							if( prefabStageOpenerWithContext != null )
							{
								prefabStageOpenerWithContext.Invoke( null, new object[2] { openPrefabStageAssetPath, openPrefabStageContextObject } );
								shouldOpenPrefabStageWithoutContext = false;
							}
						}
						catch { }
					}

					if( shouldOpenPrefabStageWithoutContext )
#endif
					{
						AssetDatabase.OpenAsset( AssetDatabase.LoadAssetAtPath<GameObject>( openPrefabStageAssetPath ) );
					}
				}
#endif
			}
		}

		private void InitializeSearchResultNodes( List<SearchResultGroup> searchResult )
		{
			for( int i = 0; i < searchResult.Count; i++ )
				searchResult[i].InitializeNodes( GetReferenceNode );

			// If there are any empty groups after node initialization, remove those groups
			for( int i = searchResult.Count - 1; i >= 0; i-- )
			{
				if( !searchResult[i].PendingSearch && searchResult[i].NumberOfReferences == 0 )
					searchResult.RemoveAtFast( i );
			}
		}

		// Checks if object is asset or scene object and adds it to the corresponding HashSet(s)
		private void AddSearchedObjectToFilteredSets( Object obj, bool expandGameObjects )
		{
			if( obj == null || obj.Equals( null ) )
				return;

			objectsToSearchSet.Add( obj );

#if UNITY_2018_3_OR_NEWER
			// When searching for references of a prefab stage object, try adding its corresponding prefab asset to the searched assets, as well
			if( openPrefabStage != null && openPrefabStagePrefabAsset != null && obj is GameObject && openPrefabStage.IsPartOfPrefabContents( (GameObject) obj ) )
			{
				GameObject prefabStageObjectSource = ( (GameObject) obj ).FollowSymmetricHierarchy( openPrefabStage.prefabContentsRoot, openPrefabStagePrefabAsset );
				if( prefabStageObjectSource != null )
					AddSearchedObjectToFilteredSets( prefabStageObjectSource, expandGameObjects );
			}
#endif

			bool isAsset = obj.IsAsset();
			if( isAsset )
			{
				assetsToSearchSet.Add( obj );

				string assetPath = AssetDatabase.GetAssetPath( obj );
				if( !string.IsNullOrEmpty( assetPath ) )
				{
					assetsToSearchPathsSet.Add( assetPath );
					if( dontSearchInSourceAssets && AssetDatabase.IsMainAsset( obj ) )
						excludedAssetsPathsSet.Add( assetPath );
				}

				GameObject go = null;
				if( obj is GameObject )
					go = (GameObject) obj;
				else if( obj is Component )
					go = ( (Component) obj ).gameObject;

				if( go != null )
				{
					Transform transform = go.transform;
					bool shouldAddRootPrefabEntry = true;
					for( int i = assetsToSearchRootPrefabs.Count - 1; i >= 0; i-- )
					{
						Transform rootTransform = assetsToSearchRootPrefabs[i].transform;
						if( transform.IsChildOf( rootTransform ) )
						{
							shouldAddRootPrefabEntry = false;
							break;
						}

						if( rootTransform.IsChildOf( transform ) )
							assetsToSearchRootPrefabs.RemoveAt( i );
					}

					if( shouldAddRootPrefabEntry )
						assetsToSearchRootPrefabs.Add( go );
				}
			}
			else
			{
				if( obj is GameObject )
					sceneObjectsToSearchScenesSet.Add( ( (GameObject) obj ).scene.path );
				else if( obj is Component )
					sceneObjectsToSearchScenesSet.Add( ( (Component) obj ).gameObject.scene.path );
			}

			if( expandGameObjects && obj is GameObject )
			{
				// If searched asset is a GameObject, include its components in the search
				Component[] components = ( (GameObject) obj ).GetComponents<Component>();
				for( int i = 0; i < components.Length; i++ )
				{
					if( components[i] == null || components[i].Equals( null ) )
						continue;

					objectsToSearchSet.Add( components[i] );

					if( isAsset )
						assetsToSearchSet.Add( components[i] );
				}
			}
			else if( obj is Component )
			{
				// Include searched components' GameObjects in the search, as well
				AddSearchedObjectToFilteredSets( ( (Component) obj ).gameObject, false );
			}
		}

		// Search a scene for references
		private void SearchScene( string scenePath, List<SearchResultGroup> searchResult, bool lazySearch, SceneSetup[] initialSceneSetup )
		{
			Scene scene = EditorSceneManager.GetSceneByPath( scenePath );
			if( isInPlayMode && !scene.isLoaded )
				return;

			bool canContainSceneObjectReference = scene.isLoaded && ( !EditorSceneManager.preventCrossSceneReferences || sceneObjectsToSearchScenesSet.Contains( scenePath ) );
			if( !canContainSceneObjectReference )
			{
				bool canContainAssetReference = assetsToSearchSet.Count > 0 && ( isInPlayMode || AssetHasAnyReference( scenePath ) );
				if( !canContainAssetReference )
					return;
			}

			if( !scene.isLoaded )
			{
				if( lazySearch )
				{
					searchResult.Add( new SearchResultGroup( scenePath, SearchResultGroup.GroupType.Scene, true, true ) );
					return;
				}

				scene = EditorSceneManager.OpenScene( scenePath, OpenSceneMode.Additive );
			}

			currentSearchResultGroup = new SearchResultGroup( scenePath, SearchResultGroup.GroupType.Scene );

			// Search through all the GameObjects in the scene
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			for( int i = 0; i < rootGameObjects.Length; i++ )
				SearchGameObjectRecursively( rootGameObjects[i] );

			// If no references are found in the scene and if the scene is not part of the initial scene setup, close it
			if( currentSearchResultGroup.NumberOfReferences == 0 )
			{
				if( !isInPlayMode )
				{
					bool sceneIsOneOfInitials = false;
					for( int i = 0; i < initialSceneSetup.Length; i++ )
					{
						if( initialSceneSetup[i].path == scenePath )
						{
							if( !initialSceneSetup[i].isLoaded )
								EditorSceneManager.CloseScene( scene, false );

							sceneIsOneOfInitials = true;
							break;
						}
					}

					if( !sceneIsOneOfInitials )
						EditorSceneManager.CloseScene( scene, true );
				}
			}
			else
			{
				// Some references are found in this scene, save the results
				searchResult.Add( currentSearchResultGroup );
			}
		}

		// Search a GameObject and its children for references recursively
		private void SearchGameObjectRecursively( GameObject go )
		{
			BeginSearchObject( go );

			Transform tr = go.transform;
			for( int i = 0; i < tr.childCount; i++ )
				SearchGameObjectRecursively( tr.GetChild( i ).gameObject );
		}

		// Begin searching a root object (like a GameObject or an asset)
		private void BeginSearchObject( Object obj )
		{
			if( obj is SceneAsset )
				return;

			currentSearchedObject = obj;

			ReferenceNode searchResult = SearchObject( obj );
			if( searchResult != null )
				currentSearchResultGroup.AddReference( searchResult );
		}

		// Search an object for references
		private ReferenceNode SearchObject( object obj )
		{
			if( obj == null || obj.Equals( null ) )
				return null;

			// Avoid recursion (which leads to stackoverflow exception) using a stack
			if( callStack.ContainsFast( obj ) )
				return null;

			bool searchingSourceAsset = searchingSourceAssets && ReferenceEquals( currentSearchedObject, obj );

			// Hashing does not work well with structs all the time, don't cache search results for structs
			string objHash = null;
			if( !( obj is ValueType ) )
			{
				objHash = obj.Hash();

				if( !searchingSourceAsset )
				{
					// If object was searched before, return the cached result
					ReferenceNode cachedResult;
					if( searchedObjects.TryGetValue( objHash, out cachedResult ) )
						return cachedResult;
				}
			}

			searchedObjectsCount++;

			ReferenceNode result;
			Object unityObject = obj as Object;
			if( unityObject != null )
			{
				// If the Object is an asset, search it in detail only if its dependencies contain at least one of the searched asset(s)
				string assetPath = null;
				if( unityObject.IsAsset() )
				{
					if( assetsToSearchSet.Count == 0 )
					{
						searchedObjects.Add( objHash, null );
						return null;
					}

					assetPath = AssetDatabase.GetAssetPath( unityObject );
					if( !AssetHasAnyReference( assetPath ) )
					{
						searchedObjects.Add( objHash, null );
						return null;
					}
				}

				callStack.Add( unityObject );

				// Search the Object in detail
				Func<Object, ReferenceNode> searchFunction;
				if( assetPath != null && extensionToSearchFunction.TryGetValue( Utilities.GetFileExtension( assetPath ), out searchFunction ) && AssetDatabase.IsMainAsset( unityObject ) )
					result = searchFunction( unityObject );
				else if( typeToSearchFunction.TryGetValue( unityObject.GetType(), out searchFunction ) )
					result = searchFunction( unityObject );
				else if( unityObject is Component )
					result = SearchComponent( unityObject );
				else
				{
					result = PopReferenceNode( unityObject );
					SearchVariablesWithSerializedObject( result );
				}

				callStack.RemoveAt( callStack.Count - 1 );
			}
			else
			{
				// Comply with the recursive search limit
				if( currentDepth >= searchDepthLimit )
					return null;

				callStack.Add( obj );
				currentDepth++;

				result = PopReferenceNode( obj );
				SearchVariablesWithReflection( result );

				currentDepth--;
				callStack.RemoveAt( callStack.Count - 1 );
			}

			if( result != null && result.NumberOfOutgoingLinks == 0 )
			{
				PoolReferenceNode( result );
				result = null;
			}

			// Cache the search result if we are skimming through a class (not a struct; i.e. objHash != null)
			// and if the object is a UnityEngine.Object (if not, cache the result only if we have actually found something
			// or we are at the root of the search; i.e. currentDepth == 0)
			if( objHash != null && ( result != null || unityObject != null || currentDepth == 0 ) )
			{
				if( !searchingSourceAsset )
					searchedObjects.Add( objHash, result );
				else if( result != null )
				{
					result.CopyReferencesTo( searchedObjects[objHash] );
					PoolReferenceNode( result );
				}
			}

			return result;
		}

		// Check if the asset at specified path depends on any of the references
		private bool AssetHasAnyReference( string assetPath )
		{
			if( assetsToSearchPathsSet.Contains( assetPath ) )
				return true;

			if( alwaysSearchedExtensionsSet.Count > 0 && alwaysSearchedExtensionsSet.Contains( Utilities.GetFileExtension( assetPath ) ) )
				return true;

			return AssetHasAnyReferenceInternal( assetPath );
		}

		// Recursively check if the asset at specified path depends on any of the references
		private bool AssetHasAnyReferenceInternal( string assetPath )
		{
			CacheEntry cacheEntry;
			if( !assetDependencyCache.TryGetValue( assetPath, out cacheEntry ) )
			{
				cacheEntry = new CacheEntry( assetPath );
				assetDependencyCache[assetPath] = cacheEntry;
			}
			else if( !cacheEntry.verified )
				cacheEntry.Verify( assetPath );

			if( cacheEntry.searchResult != CacheEntry.Result.Unknown )
				return cacheEntry.searchResult == CacheEntry.Result.Yes;

			cacheEntry.searchResult = CacheEntry.Result.No;

			string[] dependencies = cacheEntry.dependencies;
			long[] fileSizes = cacheEntry.fileSizes;
			for( int i = 0; i < dependencies.Length; i++ )
			{
				// If a dependency was renamed (which doesn't affect the verified hash, unfortunately),
				// force refresh the asset's dependencies and search it again
				if( !Directory.Exists( dependencies[i] ) ) // Calling FileInfo.Length on a directory throws FileNotFoundException
				{
					FileInfo assetFile = new FileInfo( dependencies[i] );
					if( !assetFile.Exists || assetFile.Length != fileSizes[i] )
					{
						// Although not reproduced, it is reported that this section caused StackOverflowException due to infinite loop,
						// if that happens, log useful information to help reproduce the issue
						if( lastRefreshedCacheEntry == cacheEntry )
						{
							StringBuilder sb = new StringBuilder( 1000 );
							sb.AppendLine( "<b>Infinite loop while refreshing a cache entry, please report it to the author.</b>" ).AppendLine();
							sb.Append( "Asset path: " ).AppendLine( assetPath );

							for( int j = 0; j < 2; j++ )
							{
								if( j == 1 )
								{
									cacheEntry.Refresh( assetPath );
									dependencies = cacheEntry.dependencies;
									fileSizes = cacheEntry.fileSizes;
								}

								sb.AppendLine().AppendLine( j == 0 ? "Old Dependencies:" : "New Dependencies" );
								for( int k = 0; k < dependencies.Length; k++ )
								{
									sb.Append( "- " ).Append( dependencies[k] );

									if( Directory.Exists( dependencies[k] ) )
									{
										sb.Append( " (Dir)" );
										if( fileSizes[k] != 0L )
											sb.Append( " WasCachedAsFile: " ).Append( fileSizes[k] );
									}
									else
									{
										assetFile = new FileInfo( dependencies[k] );
										sb.Append( " (File) " ).Append( "CachedSize: " ).Append( fileSizes[k] );
										if( assetFile.Exists )
											sb.Append( " RealSize: " ).Append( assetFile.Length );
										else
											sb.Append( " NoLongerExists" );
									}

									sb.AppendLine();
								}
							}

							Debug.LogError( sb.ToString() );
							return false;
						}

						cacheEntry.Refresh( assetPath );
						cacheEntry.searchResult = CacheEntry.Result.Unknown;
						lastRefreshedCacheEntry = cacheEntry;

						return AssetHasAnyReferenceInternal( assetPath );
					}
				}

				if( assetsToSearchPathsSet.Contains( dependencies[i] ) )
				{
					cacheEntry.searchResult = CacheEntry.Result.Yes;
					return true;
				}
			}

			for( int i = 0; i < dependencies.Length; i++ )
			{
				if( AssetHasAnyReferenceInternal( dependencies[i] ) )
				{
					cacheEntry.searchResult = CacheEntry.Result.Yes;
					return true;
				}
			}

			return false;
		}

		// Get reference node for object
		private ReferenceNode GetReferenceNode( object nodeObject )
		{
			ReferenceNode result;
			string hash = nodeObject.Hash();
			if( !searchedObjects.TryGetValue( hash, out result ) || result == null )
			{
				result = PopReferenceNode( nodeObject );
				searchedObjects[hash] = result;
			}

			return result;
		}

		// Fetch a reference node from pool
		private ReferenceNode PopReferenceNode( object nodeObject )
		{
			ReferenceNode node;
			if( nodesPool.Count == 0 )
				node = new ReferenceNode();
			else
			{
				int index = nodesPool.Count - 1;
				node = nodesPool[index];
				nodesPool.RemoveAt( index );
			}

			node.nodeObject = nodeObject;
			return node;
		}

		// Pool a reference node
		private void PoolReferenceNode( ReferenceNode node )
		{
			node.Clear();
			nodesPool.Add( node );
		}

		// Retrieve the game objects listed under the DontDestroyOnLoad scene
		private GameObject[] GetDontDestroyOnLoadObjects()
		{
			GameObject temp = null;
			try
			{
				temp = new GameObject();
				Object.DontDestroyOnLoad( temp );
				Scene dontDestroyOnLoad = temp.scene;
				Object.DestroyImmediate( temp );
				temp = null;

				return dontDestroyOnLoad.GetRootGameObjects();
			}
			finally
			{
				if( temp != null )
					Object.DestroyImmediate( temp );
			}
		}

		// Appends contents of callStack to StringBuilder and returns the most recent Unity object in callStack
		private Object AppendCallStackToStringBuilder( StringBuilder sb )
		{
			Object latestUnityObjectInCallStack = null;
			if( callStack.Count > 0 )
			{
				sb.AppendLine().AppendLine( "Stack contents: " );

				for( int i = callStack.Count - 1; i >= 0; i-- )
				{
					latestUnityObjectInCallStack = callStack[i] as Object;
					if( latestUnityObjectInCallStack )
					{
						if( !AssetDatabase.Contains( latestUnityObjectInCallStack ) )
						{
							string scenePath = AssetDatabase.GetAssetOrScenePath( latestUnityObjectInCallStack );
							if( !string.IsNullOrEmpty( scenePath ) && SceneManager.GetSceneByPath( scenePath ).IsValid() )
								sb.Append( "Scene: " ).AppendLine( scenePath );
						}

						break;
					}
				}

				for( int i = callStack.Count - 1; i >= 0; i-- )
				{
					sb.Append( i ).Append( ": " );

					Object unityObject = callStack[i] as Object;
					if( unityObject )
						sb.Append( unityObject.name ).Append( " (" ).Append( unityObject.GetType() ).AppendLine( ")" );
					else if( callStack[i] != null )
						sb.Append( callStack[i].GetType() ).AppendLine( " object" );
					else
						sb.AppendLine( "<<destroyed>>" );
				}

				sb.AppendLine();
			}

			return latestUnityObjectInCallStack;
		}
	}
}