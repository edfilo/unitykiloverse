using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class NearbyPOIFilter : GameObjectModifier
{
    [SerializeField] private float maxDistance = 1000f; // Only show POIs within 1000m

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        if (ve.GameObject == null) return;

        // Get distance to player
        var player = GameObject.FindFirstObjectByType<KiloFirstPersonController>();
        if (player == null)
        {
            ve.GameObject.SetActive(true); // No player found, show all POIs
            return;
        }

        float distance = Vector3.Distance(player.transform.position, ve.GameObject.transform.position);

        // Hide POIs beyond maxDistance
        if (distance > maxDistance)
        {
            ve.GameObject.SetActive(false);
        }
        else
        {
            ve.GameObject.SetActive(true);
        }
    }
}
