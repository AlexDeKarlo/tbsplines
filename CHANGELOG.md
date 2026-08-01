# Changelog

All notable changes to this package are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While
the major version is `0` the public API may still change between minor releases.

## [0.1.2] - 2026-08-01

### Changed

- The Move, Rotate and Scale shortcuts now need Ctrl: `Ctrl+G`, `Ctrl+R` and `Ctrl+E`.
  Bare letters fired while typing in the scene view and collided with Unity's own
  tool keys. Existing bindings are migrated once; rebind any of them in Settings.

### Fixed

- Height guides — the dashed lines from each point down to the grid — are on by
  default again, on new Spline Computers and on existing ones.

## [0.1.1] - 2026-08-01

### Fixed

- Importing the package no longer logs "has no meta file, but it's in an immutable
  folder" for `package.json`, `README.md`, `CHANGELOG.md` and `LICENSE.md`. Those files
  now ship with their own meta files, as Unity's own packages do.

### Removed

- The `TBSplineS/Dev` menu. It held two authoring tools that were never meant to reach
  users, and it added a top-level entry to the menu bar. Component icons are unaffected:
  they are stored in the script meta files, not assigned by that tool.

## [0.1.0] - 2026-08-01

First public release, for alpha testing.

### Added

- Spline Computer holding multiple splines, with junctions, branching and stable
  identifiers for splines and knots.
- Bezier, Catmull-Rom, B-spline and linear interpolation, with per-knot tangent modes
  and centripetal parameterization for Catmull-Rom.
- Scene-first editor: draw and edit splines directly in the scene view, with grid
  snapping, box selection, point insertion, height guides and keyboard shortcuts.
- Geometry generators: Path, Tube, Surface and Spline Mesh, with shared width, colour,
  UV, normal and mesh collider handling.
- Movement components: Spline Follower with speed regions and junction switching,
  Spline Positioner and Spline Projector.
- Spline Trigger, as both scene components and spline-owned trigger groups.
- Box Collider and Edge Collider 2D generators, and a Length Calculator with
  threshold events.
- Offset, rotation, colour and size modifiers with trapezoidal falloff regions.
- Arc-length cache for even spacing, constant-speed travel and nearest-point queries.
- XML documentation across the entire public API.
- Examples sample with nine annotated scenes.

[0.1.2]: https://github.com/AlexDeKarlo/tbsplines/releases/tag/v0.1.2
[0.1.1]: https://github.com/AlexDeKarlo/tbsplines/releases/tag/v0.1.1
[0.1.0]: https://github.com/AlexDeKarlo/tbsplines/releases/tag/v0.1.0
