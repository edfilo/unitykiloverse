using global::Mapbox.BaseModule.Data;
using global::Mapbox.BaseModule.Map;
using global::Mapbox.BaseModule.Utilities;
using global::Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using global::Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Snap terrain modifier stub - ensures mesh vertices sit on terrain.
    /// Currently a no-op; the Overture pipeline doesn't use terrain.
    /// </summary>
    public class KiloverseSnapTerrainModifier : MeshModifier
    {
        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo) { }
    }

    /// <summary>
    /// Polygon mesh modifier - triangulates polygon features into mesh geometry.
    /// Wraps the Mapbox PolygonMeshModifier.
    /// </summary>
    public class KiloversePolygonMeshModifier : MeshModifier
    {
        private readonly PolygonMeshModifier _inner;

        public KiloversePolygonMeshModifier(float height = 0f)
        {
            _inner = new PolygonMeshModifier(height);
        }

        public override void Initialize()
        {
            _inner.Initialize();
        }

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            _inner.Run(feature, md, mapInfo);
        }
    }

    /// <summary>
    /// Line mesh modifier - generates line meshes with width, join, and cap settings.
    /// Wraps the Mapbox LineMeshCore for line geometry generation.
    /// </summary>
    public class KiloverseLineMeshModifier : MeshModifier
    {
        private readonly LineMeshParameters _parameters;
        private LineMeshCore _core;

        public KiloverseLineMeshModifier(LineMeshParameters parameters)
        {
            _parameters = parameters;
        }

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            if (_core == null)
                _core = new LineMeshCore(_parameters, mapInfo);
            _core.Run(feature, md);
        }
    }

    /// <summary>
    /// Material modifier - applies materials to generated GameObjects.
    /// Supports single or dual-material (wall + emissive) setup.
    /// </summary>
    public class KiloverseMaterialModifier : GameObjectModifier
    {
        private readonly Material _primaryMaterial;
        private readonly Material _secondaryMaterial;

        public KiloverseMaterialModifier(Material primary, Material secondary = null)
        {
            _primaryMaterial = primary;
            _secondaryMaterial = secondary;
        }

        public override void Run(VectorEntity ve, IMapInformation mapInformation)
        {
            if (ve?.GameObject == null) return;

            var renderers = ve.GameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (_secondaryMaterial != null)
                {
                    renderer.materials = new Material[] { _primaryMaterial, _secondaryMaterial };
                }
                else
                {
                    renderer.material = _primaryMaterial;
                }
            }
        }
    }
}
