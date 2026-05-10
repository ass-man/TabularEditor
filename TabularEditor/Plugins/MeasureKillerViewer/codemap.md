# MeasureKillerViewer Codemap

## Purpose
Provides a runtime plugin window for inspecting Measure Killer JSON exports, lineage usage, and report visual impact from inside Tabular Editor.

## Files

### MeasureKillerViewerPlugin.csx
- Plugin entrypoint.
- Implements IRuntimeWindowPlugin.CreateWindow.
- Constructs and returns MeasureKillerViewerForm.

### MeasureKillerViewerForm.UI.csx
- UI composition and window scaffolding (MeasureKillerViewerForm, partial).
- Owns fields, constructor, toolbar/filter/grid/tab setup, event wiring, and context menu wiring.

### MeasureKillerViewerForm.Logic.csx
- Data/model logic for the same form (partial class).
- Owns JSON loading, lineage graph build/classification, filtering, details rendering, preview computation, and selection synchronization.

### MeasureKillerViewerTypes.csx
- Shared domain and helper types:
  - EffectiveUsage, LineageEdgeKind, ImpactKind enums.
  - LineageObject, LineageEdge, MkNode, VisualBox models.
  - Json parser/reader utility.

## Runtime Flow
1. Plugin host invokes MeasureKillerViewerPlugin.CreateWindow.
2. MeasureKillerViewerForm UI initializes.
3. Logic layer resolves latest export, parses JSON, builds lineage objects, and applies filters.
4. UI refreshes grid/tree/details/preview based on selection and filters.
