using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kiloverse.Mapbox
{
    // ── Geometry Types ──────────────────────────────────────────────
    public enum GeomType
    {
        UNKNOWN = 0,
        POINT = 1,
        LINESTRING = 2,
        POLYGON = 3
    }

    // ── Map Information ──────────────────────────────────────────────

    public interface IMapInformation
    {
        LatitudeLongitude LatitudeLongitude { get; }
        float Pitch { get; }
        float Bearing { get; }
        float Scale { get; }
        int AbsoluteZoom { get; }
        float Zoom { get; }
        Vector2d CenterMercator { get; }
        float GetLatitudeCompensationForLocation { get; }
        float GetScaleFor(float zoomValue);

        void Initialize();
        void Initialize(LatitudeLongitude latitudeLongitude);
        void SetLatitudeLongitude(LatitudeLongitude latlng);
        void SetInformation(LatitudeLongitude? latlng, float? zoom = null, float? pitch = null, float? bearing = null, float? scale = null);

        event Action<IMapInformation> SetView;
        event Action<IMapInformation> ViewChanged;
        event Action<IMapInformation> LatitudeLongitudeChanged;
        event Action<IMapInformation> WorldScaleChanged;

        Func<TileId, float, float, float> QueryElevation { get; set; }
    }

    // ── Mesh Data ────────────────────────────────────────────────────

    public class MeshData
    {
        public VectorFeatureUnity Feature;
        public Vector3 PositionInTile;
        public List<int> Edges;

        public List<Vector3> Vertices;
        public List<Vector3> Normals;
        public List<Vector4> Tangents;
        public List<List<int>> Triangles;
        public List<List<Vector2>> UV;
        public List<Color> Colors;

        public MeshData()
        {
            Edges = new List<int>();
            Vertices = new List<Vector3>();
            Normals = new List<Vector3>();
            Tangents = new List<Vector4>();
            Triangles = new List<List<int>>();
            UV = new List<List<Vector2>>();
            UV.Add(new List<Vector2>());
            Colors = new List<Color>();
        }

        public void Clear()
        {
            Edges.Clear();
            Vertices.Clear();
            Normals.Clear();
            Tangents.Clear();
            Colors.Clear();
            foreach (var item in Triangles) item.Clear();
            foreach (var item in UV) item.Clear();
        }
    }

    // ── Vector Feature ───────────────────────────────────────────────

    public class VectorFeatureUnity
    {
        public TileId TileId;
        public object Data; // raw tile feature data (opaque)
        public Dictionary<string, object> Properties;
        public List<List<Vector3>> Points = new List<List<Vector3>>();
        /// <summary>Feature ID from the MVT tile (used for deterministic seeding)</summary>
        public long FeatureId;
    }

    // ── Vector Entity ────────────────────────────────────────────────

    public class VectorEntity
    {
        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public Mesh Mesh;
        public MeshRenderer MeshRenderer;
        public VectorFeatureUnity Feature;
        public Transform Transform;
        public int StackId;
    }

    // ── Unity Context ────────────────────────────────────────────────

    [Serializable]
    public class UnityContext
    {
        [NonSerialized] public MonoBehaviour CoroutineStarter;
        public Transform MapRoot;
        public Transform BaseTileRoot;
        public Transform RuntimeGenerationRoot;

        public void Initialize()
        {
            BaseTileRoot = BaseTileRoot == null ? new GameObject("BaseTiles").transform : BaseTileRoot;
            if (MapRoot != null) BaseTileRoot.SetParent(MapRoot);
            BaseTileRoot.transform.localPosition = Vector3.zero;

            RuntimeGenerationRoot = RuntimeGenerationRoot == null ? new GameObject("RuntimeObjectsRoot").transform : RuntimeGenerationRoot;
            if (MapRoot != null) RuntimeGenerationRoot.SetParent(MapRoot);
            RuntimeGenerationRoot.transform.localPosition = Vector3.zero;
        }

        public void OnDestroy() { }
    }

    // ── Modifier Interfaces ──────────────────────────────────────────

    public interface IMeshModifier
    {
        void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo);
        void Initialize();
    }

    public interface IGameObjectModifier
    {
        void Run(VectorEntity ve, IMapInformation mapInformation);
        void OnPoolItem(VectorEntity vectorEntity);
        void Clear();
        void ClearCaches();
        void Unregister(TileId tileId);
        void Finalize(VectorEntity entity);
    }

    public interface IPolygonMeshModifier
    {
        void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo);
    }

    // ── Modifier Base Classes ────────────────────────────────────────

    [Serializable]
    public class ModifierBase { }

    [Serializable]
    public class MeshModifier : ModifierBase, IMeshModifier
    {
        public virtual void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo) { }
        public virtual void Initialize() { }
    }

    [Serializable]
    public class GameObjectModifier : ModifierBase, IGameObjectModifier
    {
        public virtual void Run(VectorEntity ve, IMapInformation mapInformation) { }
        public virtual void OnPoolItem(VectorEntity vectorEntity) { }
        public virtual void Clear() { }
        public virtual void ClearCaches() { }
        public virtual void Unregister(TileId tileId) { }
        public virtual void Finalize(VectorEntity entity) { }
    }

    // ── ScriptableObject Wrappers ────────────────────────────────────

    public abstract class ScriptableMeshModifierObject : ScriptableObject, IMeshModifier
    {
        protected abstract MeshModifier _meshModifierImplementation { get; }

        public virtual void ConstructModifier(UnityContext unityContext) { }

        public virtual void Initialize()
        {
            _meshModifierImplementation.Initialize();
        }

        public void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            _meshModifierImplementation.Run(feature, md, mapInfo);
        }
    }

    public abstract class ScriptableGameObjectModifierObject : ScriptableObject, IGameObjectModifier
    {
        protected abstract GameObjectModifier _gameObjectModifierImplementation { get; }

        public abstract void ConstructModifier(UnityContext unityContext);

        public void Run(VectorEntity ve, IMapInformation mapInformation)
        {
            _gameObjectModifierImplementation.Run(ve, mapInformation);
        }

        public void OnPoolItem(VectorEntity vectorEntity)
        {
            _gameObjectModifierImplementation.OnPoolItem(vectorEntity);
        }

        public void Clear()
        {
            _gameObjectModifierImplementation.Clear();
        }

        public void ClearCaches()
        {
            _gameObjectModifierImplementation.ClearCaches();
        }

        public void Unregister(TileId tileId)
        {
            _gameObjectModifierImplementation.Unregister(tileId);
        }

        public void Finalize(VectorEntity entity)
        {
            _gameObjectModifierImplementation.Finalize(entity);
        }
    }

    // ── Object Pool ─────────────────────────────────────────────────

    public class ObjectPool<T>
    {
        private Queue<T> _objects;
        private Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator, int initialItemCount = 0)
        {
            _objects = new Queue<T>();
            _objectGenerator = objectGenerator;
            for (int i = 0; i < initialItemCount; i++)
                _objects.Enqueue(_objectGenerator());
        }

        public IEnumerator InitializeItems(int count)
        {
            for (int i = 0; i < count; i++)
                _objects.Enqueue(_objectGenerator());
            return null;
        }

        public T GetObject()
        {
            return _objects.Count > 0 ? _objects.Dequeue() : _objectGenerator();
        }

        public void Put(T item) => _objects.Enqueue(item);
        public void Clear() => _objects.Clear();
    }

    // ── Mesh Extensions ─────────────────────────────────────────────

    public static class MeshExtensions
    {
        public static void SetMeshValues(this Mesh mesh, MeshData data)
        {
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = data.Triangles.Count;
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            if (data.Tangents.Count > 0)
                mesh.SetTangents(data.Tangents);

            var counter = data.Triangles.Count;
            mesh.subMeshCount = counter;
            for (int i = 0; i < counter; i++)
                mesh.SetTriangles(data.Triangles[i], i);

            counter = data.UV.Count;
            for (int i = 0; i < counter; i++)
                mesh.SetUVs(i, data.UV[i]);
        }
    }

    // ── Modifier Stack ──────────────────────────────────────────────

    public enum LayerTypeEnum { Polygon, Line, Point }

    [Serializable]
    public class ModifierStackSettings
    {
        public Vector2 VisibilityZoomRange = new Vector2(1, 20);
        public bool MergeObjects = true;
        public LayerTypeEnum LayerType;
    }

    [Serializable]
    public class ModifierStack
    {
        public List<IMeshModifier> MeshModifiers;
        public List<IGameObjectModifier> GoModifiers;
        public ModifierStackSettings Settings;
        private ObjectPool<VectorEntity> _objectPool;

        public ModifierStack(ModifierStackSettings settings)
        {
            Settings = settings;
            MeshModifiers = new List<IMeshModifier>();
            GoModifiers = new List<IGameObjectModifier>();
        }

        public void Initialize(Transform layerRootObject = null)
        {
            _objectPool = new ObjectPool<VectorEntity>(() =>
            {
                var go = new GameObject("pool item");
                if (layerRootObject != null)
                    go.transform.SetParent(layerRootObject, false);
                return new VectorEntity { GameObject = go, Transform = go.transform };
            });
            _objectPool.InitializeItems(20);
        }

        public MeshData RunMeshModifiers(VectorFeatureUnity feature, MeshData meshData, IMapInformation mapInfo)
        {
            for (int i = 0; i < MeshModifiers.Count; i++)
                if (MeshModifiers[i] != null)
                    MeshModifiers[i].Run(feature, meshData, mapInfo);
            return meshData;
        }

        public void RunGoModifiers(VectorEntity entity, IMapInformation mapInformation)
        {
            for (int i = 0; i < GoModifiers.Count; i++)
                GoModifiers[i].Run(entity, mapInformation);
        }

        public VectorEntity CreateEntity(MeshData meshData)
        {
            var entity = _objectPool.GetObject();
            if (entity.GameObject == null)
                return null;

            if (meshData.Vertices.Count > 0)
            {
                if (entity.MeshFilter == null)
                {
                    entity.MeshFilter = entity.GameObject.AddComponent<MeshFilter>();
                    entity.Mesh = new Mesh();
                    entity.MeshFilter.mesh = entity.Mesh;
                }
                entity.Mesh.Clear();
                entity.Mesh.SetMeshValues(meshData);
            }
            else if (entity.Mesh != null)
            {
                entity.Mesh.Clear();
            }

            entity.GameObject.name = "go";
            entity.GameObject.SetActive(false);
            entity.Transform.localPosition = meshData.PositionInTile;
            return entity;
        }

        public void UnregisterTile(TileId tileId)
        {
            foreach (var goModifier in GoModifiers)
                goModifier.Unregister(tileId);
        }

        public void Finalize(VectorEntity entity)
        {
            foreach (var goModifier in GoModifiers)
                goModifier.Finalize(entity);
            _objectPool.Put(entity);
        }

        public void OnDestroy() => _objectPool?.Clear();

        public bool IsZinSupportedRange(int targetZ)
        {
            return Settings.VisibilityZoomRange.x <= targetZ && Settings.VisibilityZoomRange.y >= targetZ;
        }
    }

    // ── ModifierStackObject (ScriptableObject) ──────────────────────

    [CreateAssetMenu(menuName = "Kiloverse/Modifiers/Modifier Stack")]
    public class ModifierStackObject : ScriptableObject
    {
        public ModifierStackSettings Settings;
        public List<ScriptableMeshModifierObject> MeshModifiers = new List<ScriptableMeshModifierObject>();
        public List<ScriptableGameObjectModifierObject> GoModifiers = new List<ScriptableGameObjectModifierObject>();
        private ModifierStack _modifierStack;

        public ModifierStack GetModifierStack => _modifierStack;

        public void Initialize(UnityContext unityContext = null)
        {
            _modifierStack = new ModifierStack(Settings ?? new ModifierStackSettings());
            foreach (var meshMod in MeshModifiers)
            {
                if (meshMod != null)
                {
                    meshMod.ConstructModifier(unityContext);
                    _modifierStack.MeshModifiers.Add(meshMod);
                }
            }
            foreach (var goMod in GoModifiers)
            {
                if (goMod != null)
                {
                    goMod.ConstructModifier(unityContext);
                    _modifierStack.GoModifiers.Add(goMod);
                }
            }
        }

        public MeshData RunMeshModifiers(VectorFeatureUnity feature, MeshData meshData, IMapInformation mapInfo)
        {
            if (_modifierStack == null) Initialize();
            return _modifierStack.RunMeshModifiers(feature, meshData, mapInfo);
        }

        public void RunGoModifiers(VectorEntity entity, IMapInformation mapInformation)
        {
            if (_modifierStack == null) Initialize();
            _modifierStack.RunGoModifiers(entity, mapInformation);
        }
    }

    // ── Visualizer Types ────────────────────────────────────────────

    public enum ModifierStackExecutionMode { All, FirstHit }

    [Serializable]
    public class VectorLayerVisualizerSettings
    {
        public ModifierStackExecutionMode StackExecutionMode = ModifierStackExecutionMode.All;
    }

    public interface IVectorLayerVisualizer
    {
        string VectorLayerName { get; }
        bool Active { get; set; }
        Dictionary<int, ModifierStack> GetModStacks { get; }
        void AddModifierStack(List<ModifierStack> stack);
        Dictionary<int, HashSet<MeshData>> CreateMesh(TileId tileId, object layer);
        List<GameObject> CreateGo(TileId tileId, Dictionary<int, HashSet<MeshData>> meshData);
        void UnregisterTile(TileId tileId);
        IEnumerator Initialize();
        void OnDestroy();
        void UpdateForView(TileId tileId, IMapInformation information);
        void SetActive(TileId tileId, bool isActive, IMapInformation mapInformation);
        bool ContainsVisualFor(TileId dataTileId);
    }

    [Serializable]
    public class VectorLayerVisualizer : IVectorLayerVisualizer
    {
        protected string _vectorLayerName;
        protected UnityContext _unityContext;
        protected Dictionary<int, ModifierStack> _stackList = new Dictionary<int, ModifierStack>();
        protected Dictionary<TileId, List<VectorEntity>> _results = new Dictionary<TileId, List<VectorEntity>>();
        protected IMapInformation _mapInformation;
        protected Transform _layerRootObject;

        public Dictionary<int, ModifierStack> GetModStacks => _stackList;
        public string VectorLayerName => _vectorLayerName;
        public bool Active { get; set; } = true;

        public Action<GameObject> OnVectorMeshCreated = delegate { };
        public Action<GameObject> OnVectorMeshDestroyed = delegate { };

        public VectorLayerVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null, VectorLayerVisualizerSettings settings = null)
        {
            _vectorLayerName = name;
            _mapInformation = mapInformation;
            _unityContext = unityContext;
        }

        public virtual IEnumerator Initialize()
        {
            foreach (var stack in _stackList)
                stack.Value.Initialize(_layerRootObject);
            yield return null;
        }

        public void AddModifierStack(List<ModifierStack> stack)
        {
            foreach (var modifierStack in stack)
                _stackList.Add(modifierStack.GetHashCode(), modifierStack);
        }

        public virtual Dictionary<int, HashSet<MeshData>> CreateMesh(TileId tileId, object layer) => new Dictionary<int, HashSet<MeshData>>();

        public virtual List<GameObject> CreateGo(TileId tileId, Dictionary<int, HashSet<MeshData>> meshData)
        {
            var objectList = new List<GameObject>();
            foreach (var pair in meshData)
            {
                foreach (var md in pair.Value)
                {
                    if (!_stackList.ContainsKey(pair.Key)) continue;
                    var entity = _stackList[pair.Key].CreateEntity(md);
                    if (entity == null || entity.GameObject == null) continue;

                    entity.GameObject.transform.SetParent(_layerRootObject);
                    entity.StackId = pair.Key;
                    entity.Feature = md.Feature;
                    if (UnityEngine.Application.isEditor)
                        entity.GameObject.name = VectorLayerName + " " + tileId.ToString();
                    _stackList[pair.Key].RunGoModifiers(entity, _mapInformation);
                    objectList.Add(entity.GameObject);

                    if (!_results.ContainsKey(tileId))
                        _results.Add(tileId, new List<VectorEntity>());
                    _results[tileId].Add(entity);
                    OnVectorMeshCreated(entity.GameObject);
                }
            }
            return objectList;
        }

        public virtual void UnregisterTile(TileId tileId)
        {
            if (_results.ContainsKey(tileId))
            {
                foreach (var entity in _results[tileId])
                {
                    entity.GameObject.SetActive(false);
                    if (_stackList.TryGetValue(entity.StackId, out var stack))
                        stack.Finalize(entity);
                    OnVectorMeshDestroyed(entity.GameObject);
                }
                _results[tileId].Clear();
                _results.Remove(tileId);
            }
            foreach (var modifierStack in _stackList.Values)
                modifierStack.UnregisterTile(tileId);
        }

        public void OnDestroy()
        {
            foreach (var entities in _results.Values)
                foreach (var entity in entities)
                {
                    OnVectorMeshDestroyed(entity.GameObject);
                    GameObject.Destroy(entity.GameObject);
                }
            foreach (var stack in _stackList)
                stack.Value.OnDestroy();
            _results.Clear();
        }

        public virtual void UpdateForView(TileId tileId, IMapInformation information) { }
        public virtual void SetActive(TileId tileId, bool isActive, IMapInformation mapInformation) { }
        public bool ContainsVisualFor(TileId dataTileId) => _results.ContainsKey(dataTileId);
    }

    // ── VectorLayerVisualizerObject (ScriptableObject) ──────────────

    // VectorLayerModuleScript, VectorLayerVisualizerObject, LayerVisualizerConstructor
    // are NOT defined here — they come from the Mapbox SDK package (mapbox-unity-sdk-v3).
    // The scene has a VectorLayerModuleScript component with serialized visualizer configs.
    // OvertureMapManager uses FindFirstObjectByType to find the Mapbox component at runtime.
}

