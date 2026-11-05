using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.UnitLogic.Abilities.Components.Patterns;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace RogueTraderOverwatch;

// A bright blue line along the tile edges of an Overwatch cone, while aiming and while it's active, since the
// game's own area highlight (a shared red fill used for every hostile area) is hard to see. Pure code, no Unity
// asset: LineRenderers with Unity's built-in Hidden/Internal-Colored shader. Every side of a cell in the area
// that borders a cell outside it is an edge; the edges are chained into closed loops in grid space (an area can
// have several, e.g. around a hole) and each corner is placed in the world through the grid's own transform at
// its cell's height.
internal static class AbilityOutline
{
    private const float LineWidth = 0.025f;
    private const float GroundOffset = 0.06f;
    private const float MinStep = 0.05f; // height difference (grid units) that gets a vertical connector
    public static readonly Color OverwatchColor = new Color(0.25f, 0.6f, 1f, 0.5f); // 50% transparent

    private static readonly Dictionary<string, List<LineRenderer>> Active = new();

    // One thin LineRenderer per boundary tile edge, pooled per caster; extras are disabled, not destroyed.
    public static void Show(BaseUnitEntity caster, IReadOnlyCollection<CustomGridNodeBase> area, Color color)
    {
        var edges = Edges(area);
        if (edges.Count == 0)
        {
            Hide(caster);
            return;
        }
        if (!Active.TryGetValue(caster.UniqueId, out var lines))
            Active[caster.UniqueId] = lines = new List<LineRenderer>();
        while (lines.Count < edges.Count)
            lines.Add(Create());
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null)
                lines[i] = line = Create();
            if (i >= edges.Count)
            {
                line.enabled = false;
                continue;
            }
            line.enabled = true;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, edges[i].A);
            line.SetPosition(1, edges[i].B);
        }
    }

    // The live targeting preview (GetOrientedPattern, every time the aimed area changes). Sync keeps it on screen for
    // as long as the player is still aiming this caster's ability, however long the cursor sits still.
    public static void ShowPreview(BaseUnitEntity caster, OrientedPatternData pattern, Color color)
    {
        // GetOrientedPattern also runs for the AI weighing targets and for the cast itself; only the ability the
        // player is actually aiming is a preview.
        if (Game.Instance.SelectedAbilityHandler?.Ability?.Caster != caster)
            return;
        var cells = new List<CustomGridNodeBase>();
        foreach (var node in pattern.Nodes)
            cells.Add(node);
        Show(caster, cells, color);
    }

    private const float ActiveRefresh = 0.25f;
    private static readonly Dictionary<string, float> LastActiveRefresh = new();

    // Called every frame (VisualsDriver): an active Overwatch keeps its outline on the locked zone; the ability
    // currently being aimed (Game.SelectedAbilityHandler) keeps its preview; everything else is hidden. Reflex
    // Shot no longer arms a zone or has a facing to aim (RTO-T111), so it never shows here.
    public static void Sync()
    {
        var aimed = Game.Instance.SelectedAbilityHandler?.Ability;
        var aimer = aimed != null && OverwatchCone.IsOurs(aimed) ? aimed.Caster as BaseUnitEntity : null;
        var keep = new HashSet<string>();
        foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
        {
            var part = unit.GetOptional<PartOverwatch>();
            if (part != null && !part.IsStopped && OverwatchCone.IsOurs(part.Ability))
            {
                keep.Add(unit.UniqueId);
                if (!LastActiveRefresh.TryGetValue(unit.UniqueId, out var at) || Time.unscaledTime - at >= ActiveRefresh)
                {
                    LastActiveRefresh[unit.UniqueId] = Time.unscaledTime;
                    Show(unit, part.OverwatchArea, OverwatchColor);
                }
            }
            else if (unit == aimer)
            {
                keep.Add(unit.UniqueId);
            }
        }
        foreach (var id in new List<string>(Active.Keys))
            if (!keep.Contains(id))
                HideById(id);
    }

    public static void Hide(BaseUnitEntity caster) => HideById(caster.UniqueId);

    private static void HideById(string id)
    {
        LastActiveRefresh.Remove(id);
        if (!Active.TryGetValue(id, out var lines))
            return;
        Active.Remove(id);
        foreach (var line in lines)
            if (line != null)
                UnityEngine.Object.Destroy(line.gameObject);
    }

    private static LineRenderer Create()
    {
        var go = new GameObject("RogueTraderOverwatch_Outline");
        var line = go.AddComponent<LineRenderer>();
        line.loop = false;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = LineWidth;
        // Hidden/Internal-Colored is what Unity's own debug lines use and is always in a player build.
        line.sharedMaterial = SharedMaterial();
        line.numCornerVertices = 0;
        return line;
    }

    // Every side of a cell in the area that borders a cell outside it, as a world-space segment at that cell's
    // height. Cell (x, z) spans [x, x+1] x [z, z+1] in grid space; the grid's own transform places it in the world.
    private static List<(Vector3 A, Vector3 B)> Edges(IReadOnlyCollection<CustomGridNodeBase> area)
    {
        var result = new List<(Vector3, Vector3)>();
        CustomGridGraph graph = null;
        var cells = new Dictionary<Vector2Int, float>(); // grid coords -> graph-space height
        foreach (var node in area)
        {
            graph ??= CustomGridNode.GetGridGraph(node.GraphIndex);
            cells[node.CoordinatesInGrid] = graph.transform.InverseTransform((Vector3)node.position).y;
        }
        if (graph == null)
            return result;

        Vector3 World(Vector2Int corner, float y) =>
            graph.transform.Transform(new Vector3(corner.x, y, corner.y)) + Vector3.up * GroundOffset;

        // Heights each boundary corner is drawn at; a corner shared by edges at different heights (a crate top next to
        // the floor) gets a vertical connector so the outline stays one continuous line.
        var cornerHeights = new Dictionary<Vector2Int, (float Min, float Max)>();
        void Edge(Vector2Int from, Vector2Int to, float y)
        {
            result.Add((World(from, y), World(to, y)));
            foreach (var corner in new[] { from, to })
                cornerHeights[corner] = cornerHeights.TryGetValue(corner, out var h) ? (Mathf.Min(h.Min, y), Mathf.Max(h.Max, y)) : (y, y);
        }

        foreach (var pair in cells)
        {
            var c = pair.Key;
            float y = pair.Value;
            var a = c;
            var b = c + Vector2Int.right;
            var cc = c + Vector2Int.one;
            var d = c + Vector2Int.up;
            if (!cells.ContainsKey(c + Vector2Int.down)) Edge(a, b, y);
            if (!cells.ContainsKey(c + Vector2Int.right)) Edge(b, cc, y);
            if (!cells.ContainsKey(c + Vector2Int.up)) Edge(cc, d, y);
            if (!cells.ContainsKey(c + Vector2Int.left)) Edge(d, a, y);
        }

        foreach (var pair in cornerHeights)
            if (pair.Value.Max - pair.Value.Min > MinStep)
                result.Add((World(pair.Key, pair.Value.Min), World(pair.Key, pair.Value.Max)));
        return result;
    }

    // One material for every outline line (a material per line would never be freed). Colours come from the
    // LineRenderer's vertex colours, which Hidden/Internal-Colored uses.
    private static Material s_Material;

    private static Material SharedMaterial()
    {
        if (s_Material != null)
            return s_Material;
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            PFLog.Mods.Error("RogueTrader.Overwatch: outline: no usable shader found, the outline won't draw");
        s_Material = new Material(shader) { renderQueue = 4000 };
        // Alpha-blended, so the lines' 50% vertex alpha shows the ground through them.
        s_Material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        s_Material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        s_Material.SetInt("_ZWrite", 0);
        s_Material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        UnityEngine.Object.DontDestroyOnLoad(s_Material);
        return s_Material;
    }

    public static void Log(Exception ex, string where) => PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: outline " + where);
}
