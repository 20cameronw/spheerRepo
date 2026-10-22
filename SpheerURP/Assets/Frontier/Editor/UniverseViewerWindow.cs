#if UNITY_EDITOR
using System;
using System.Globalization;
using Frontier.Universe;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public sealed class UniverseViewerWindow : EditorWindow
    {
        const int PreviewPixels = 256;
        [SerializeField] string seedText = "46524F4E54494552";
        [SerializeField] double centerX, centerY, spanLy = 30000;
        UniverseService service;
        Texture2D preview;
        StarSystem[] systems = Array.Empty<StarSystem>();
        StarSystem selectedSystem;
        Vector2 scroll;
        string notice;
        double previewX, previewY, previewSpan;
        public Texture2D Preview { get { return preview; } }
        public ulong CurrentSeed { get { return service == null ? 0 : service.Seed; } }
        public SectorCoord InspectedSector { get; private set; }
        public string SelectedDescriptor { get; private set; } = "";
        public string SelectedPlanetId { get; private set; } = "";

        [MenuItem("Frontier/Universe/Universe Viewer")]
        public static void Open()
        {
            var window = GetWindow<UniverseViewerWindow>("Universe Viewer");
            window.minSize = new Vector2(360, 440);
        }

        public bool TryRefresh(string seed, double x, double y, double span, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(seed) || seed.Length > 16 || !SeedText.TryParse(seed, out var parsed))
            {
                error = "Enter a hexadecimal 64-bit seed (up to 16 digits).";
                return false;
            }
            if (!Finite(x) || !Finite(y) || !Finite(span) || span <= 0)
            {
                error = "Coordinates must be finite and the view span must be positive.";
                return false;
            }
            var config = service != null ? service.Universe.Config : new UniverseConfig();
            double min = SectorCoord.MinCoord * config.SectorSize;
            double max = ((double)SectorCoord.MaxCoord + 1) * config.SectorSize;
            if (x - span * 0.5 < min || y - span * 0.5 < min || x + span * 0.5 >= max || y + span * 0.5 >= max)
            {
                error = "The requested view extends beyond the generator's supported coordinate range.";
                return false;
            }
            var next = service != null && service.Seed == parsed ? service : new UniverseService(GameDatabase.Load(), seed: parsed);
            var texture = UniverseTools.CreateDensityTexture(next.Universe, x, y, span, PreviewPixels);
            var sector = next.Universe.SectorOf(x, y);
            var nextSystems = next.Universe.SystemsInSector(sector);
            if (preview != null) DestroyImmediate(preview);
            preview = texture;
            service = next;
            seedText = SeedText.Format(parsed);
            centerX = x; centerY = y; spanLy = span;
            previewX = x; previewY = y; previewSpan = span;
            InspectedSector = sector;
            systems = nextSystems;
            selectedSystem = null;
            SelectedDescriptor = ""; SelectedPlanetId = "";
            return true;
        }

        public bool TrySelectPlanet(string id, out string error)
        {
            error = "";
            if (service == null) { error = "Refresh the universe preview first."; return false; }
            var planet = service.Get(id);
            if (planet == null) { error = "No planet exists at that id in the preview universe."; return false; }
            SelectedPlanetId = planet.Id;
            SelectedDescriptor = planet.Dump();
            selectedSystem = planet.Star;
            return true;
        }

        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Universe Viewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Read-only generator preview. Changing its seed or coordinates never changes the live game or a save.", MessageType.Info);
            seedText = EditorGUILayout.TextField("Seed (hex)", seedText);
            centerX = EditorGUILayout.DoubleField("X (light-years)", centerX);
            centerY = EditorGUILayout.DoubleField("Y (light-years)", centerY);
            spanLy = EditorGUILayout.DoubleField("View span (light-years)", spanLy);
            if (GUILayout.Button("Jump / Refresh"))
            {
                TryRefresh(seedText, centerX, centerY, spanLy, out notice);
                GUI.FocusControl(null);
            }
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Warning);
            if (preview == null) { EditorGUILayout.EndScrollView(); return; }

            EditorGUILayout.LabelField("Density heat map", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(512));
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, false);
            EditorGUILayout.LabelField("Displayed seed: " + SeedText.Format(service.Seed));
            EditorGUILayout.LabelField("Centre: " + previewX.ToString("0.##", CultureInfo.InvariantCulture) + ", " +
                previewY.ToString("0.##", CultureInfo.InvariantCulture) + " ly; span " +
                previewSpan.ToString("0.##", CultureInfo.InvariantCulture) + " ly");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sector " + InspectedSector.X + ", " + InspectedSector.Y, EditorStyles.boldLabel);
            var galaxy = service.Universe.GalaxyAt(previewX, previewY);
            EditorGUILayout.LabelField(galaxy == null ? "Intergalactic space" : galaxy.Name);
            EditorGUILayout.LabelField(systems.Length + " systems. Tier at centre: " + service.Universe.TierAt(previewX, previewY));
            foreach (var star in systems)
            {
                if (GUILayout.Button(star.Name + "  [" + star.Id + "]  Tier " + star.Tier, EditorStyles.miniButton))
                {
                    selectedSystem = star;
                    SelectedDescriptor = ""; SelectedPlanetId = "";
                }
            }
            if (selectedSystem != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(selectedSystem.Name + " / " + selectedSystem.StarClass, EditorStyles.boldLabel);
                foreach (var planet in service.PlanetsOf(selectedSystem))
                    if (GUILayout.Button(planet.Name + "  [" + planet.Id + "]"))
                        TrySelectPlanet(planet.Id, out notice);
            }
            if (!string.IsNullOrEmpty(SelectedDescriptor))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Planet descriptor", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(SelectedDescriptor, EditorStyles.textArea, GUILayout.MinHeight(200));
                if (GUILayout.Button("Copy descriptor")) EditorGUIUtility.systemCopyBuffer = SelectedDescriptor;
            }
            EditorGUILayout.EndScrollView();
        }

        void OnDisable()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
            service = null;
            systems = Array.Empty<StarSystem>();
        }
    }
}
#endif
