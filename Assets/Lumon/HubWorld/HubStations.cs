using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumon.HubWorld
{
    public enum HubStation
    {
        None,
        Team,
        Expeditions,
        Settings,
    }

    public enum HubRequestSource
    {
        // E, standing by the station.
        Key,
        // A click on the painted station, standing by it.
        Pointer,
        // A HUD button or shortcut. Needs no walking when the station allows it.
        Hud,
    }

    // All positions are plate pixels, measured from the top-left corner of the painting.
    [Serializable]
    public sealed class HubStationSpot
    {
        public HubStation station;
        [Tooltip("The painted prop. Where a prompt belongs.")]
        public Vector2 anchor;
        [Tooltip("What a click has to land on.")]
        public Rect pointerArea;
        [Tooltip("Point of the floor in front of the station.")]
        public Vector2 approach;
        [Tooltip("How far from the approach point the feet may be.")]
        public float reach = 60f;
        [Tooltip("The HUD may open this station from anywhere.")]
        public bool hudShortcut = true;

        public bool InReach(Vector2 feet)
        {
            return (feet - approach).sqrMagnitude <= reach * reach;
        }
    }

    // What the Hub offers to the interface: which station the guardian stands by, requests
    // to open one, and the pause of world input while a modal is up. It opens nothing itself.
    // Only the station keys and the left mouse button are read here; Space and Esc stay with the interface.
    public sealed class HubStations : MonoBehaviour
    {
        [SerializeField] private HubGround ground;
        [Tooltip("Empty: the main camera.")]
        [SerializeField] private Camera view;

        [SerializeField] private HubStationSpot[] spots = Array.Empty<HubStationSpot>();

        // The interface opens the station it is handed. LastRequestSource tells how it was asked.
        public event Action<HubStation> OnStationRequested;
        // For the "E" prompt. HubStation.None when the guardian walks away.
        public event Action<HubStation> OnNearbyStationChanged;

        // Set by the interface: true when it covers that screen position, so the click is its own.
        public Func<Vector2, bool> PointerOverInterface { get; set; }

        public HubStation NearbyStation { get; private set; }
        public HubRequestSource LastRequestSource { get; private set; }
        public IReadOnlyList<HubStationSpot> Spots => spots;
        public HubGround Ground => ground;
        public bool WorldInputPaused => ground != null && ground.InputPaused;

        // While any owner holds the pause the guardian rests in place and keyboard interaction and clicks on the
        // world are ignored. HUD requests still go through, so one modal can lead to another.
        public void SetWorldInputPaused(object owner, bool paused)
        {
            if (ground != null)
                ground.SetInputPaused(owner, paused);
        }

        public void Configure(HubGround floor, Camera lens, HubStationSpot[] stations)
        {
            ground = floor;
            view = lens;
            spots = stations;
        }

        public HubStationSpot Spot(HubStation station)
        {
            foreach (HubStationSpot spot in spots)
                if (spot != null && spot.station == station && station != HubStation.None)
                    return spot;
            return null;
        }

        public bool CanRequest(HubStation station, HubRequestSource source)
        {
            HubStationSpot spot = Spot(station);
            // Not isActiveAndEnabled: that is false outside Play Mode, where the editor check runs.
            if (spot == null || !enabled || !gameObject.activeInHierarchy)
                return false;
            if (source == HubRequestSource.Hud)
                return spot.hudShortcut;
            return ground != null && ground.Ready && !ground.InputPaused && spot.InReach(ground.FeetPlate);
        }

        // True when the request was accepted and announced.
        public bool RequestStation(HubStation station, HubRequestSource source = HubRequestSource.Hud)
        {
            if (!CanRequest(station, source))
                return false;
            LastRequestSource = source;
            OnStationRequested?.Invoke(station);
            return true;
        }

        // E: the station the guardian stands by.
        public bool Interact()
        {
            Refresh();
            return RequestStation(NearbyStation, HubRequestSource.Key);
        }

        // A click at a point of the plate.
        public bool PointerAt(Vector2 plate)
        {
            foreach (HubStationSpot spot in spots)
                if (spot != null && spot.pointerArea.Contains(plate))
                    return RequestStation(spot.station, HubRequestSource.Pointer);
            return false;
        }

        // Follows the guardian. Runs every frame, also while input is paused.
        public void Refresh()
        {
            HubStation nearest = HubStation.None;
            if (ground != null && ground.Ready)
            {
                Vector2 feet = ground.FeetPlate;
                float best = float.MaxValue;
                foreach (HubStationSpot spot in spots)
                {
                    if (spot == null || spot.station == HubStation.None || !spot.InReach(feet))
                        continue;
                    float distance = (feet - spot.approach).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = spot.station;
                    }
                }
            }
            if (nearest == NearbyStation)
                return;
            NearbyStation = nearest;
            OnNearbyStationChanged?.Invoke(nearest);
        }

        private void Update()
        {
            Refresh();
            if (WorldInputPaused)
                return;
            if (Input.GetKeyDown(NearbyStation == HubStation.Team ? KeyCode.E : KeyCode.F))
                Interact();
            if (Input.GetMouseButtonDown(0))
                Click(Input.mousePosition);
        }

        private void Click(Vector2 screen)
        {
            Camera lens = view != null ? view : Camera.main;
            if (lens == null || ground == null || !ground.Ready)
                return;
            if (PointerOverInterface != null && PointerOverInterface(screen))
                return;
            PointerAt(ground.WorldToPlate(lens.ScreenToWorldPoint(new Vector3(screen.x, screen.y, lens.nearClipPlane))));
        }
    }
}
