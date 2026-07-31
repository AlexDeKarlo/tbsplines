# Changelog

All notable changes to this package are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While
the major version is `0` the public API may still change between minor releases.

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

[0.1.0]: https://github.com/AlexDeKarlo/tbsplines/releases/tag/v0.1.0
