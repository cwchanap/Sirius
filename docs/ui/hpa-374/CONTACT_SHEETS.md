# HPA-374 contact-sheet review

Reviewed 2026-07-29. All six sheets are accepted for the generated runtime
inventory. This review checks readable 16 px silhouettes, consistent outline
weight, clean alpha edges, unclipped glow, non-colour-only state distinction,
readable disabled treatment, a seamless calibration tick strip, and undistorted
halo/frame geometry.

| Sheet | Scope | Status |
| --- | --- | --- |
| [icons-16.png](contact-sheets/icons-16.png) | Metadata icons | Accepted |
| [icons-24.png](contact-sheets/icons-24.png) | Default icons | Accepted |
| [icons-32.png](contact-sheets/icons-32.png) | Feature icons | Accepted |
| [icon-states.png](contact-sheets/icon-states.png) | Normal, disabled, and state treatment | Accepted |
| [ornaments.png](contact-sheets/ornaments.png) | 13 ornaments | Accepted |
| [effects.png](contact-sheets/effects.png) | Four UI effects | Accepted |

These are review artifacts, not runtime resources. Regenerate them with
`rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets`.
