Use case: stylized-concept
Asset type: Sirius RPG reusable celestial UI ornament master
Primary request: Generate exactly one isolated ornament described below.
Input image: HPA-373 battle-preparation reference, used only for Sirius palette, celestial line language, and anime-fantasy rendering.
Scene/backdrop: perfectly flat solid #00FF00 chroma-key background for removal.
Style/medium: crisp mystical anime-fantasy UI line ornament, celestial-navigation motif, simplified silhouette, controlled cel-shaded highlight, strong dark indigo outline.
Composition/framing: one subject on a square 1024x1024 source canvas, fully inside the centered target-aspect crop at no less than twice final resolution. Preserve generous transparent safety insets on every non-repeat edge.
Color palette: #050714, #0D1530, #18234A, #27366C, #F7F5FF, #C7CEE8, #8F9AB8, #62DCFF, #F5D784, #DFAE43, #D96CC2, #68D6A3, #F1B85B, #F16D83.
Constraints: no text, no letters, no numbers, no watermark, no panel fill, no detached particles, no cast shadow, no reflection, no outer glow, and do not use #00FF00 in the subject. Do not bake a reusable panel, responsive layout, or UI labels into an ornament.
Avoid: photorealism, generic mobile emoji, glossy app-store icon tiles, micro-detail, chrome bevels, circuitry, and baked UI labels.

| ID | Source composition | Runtime size | Required geometry |
|---|---|---:|---|
| `celestial_anchor` | Symmetrical compass-star anchor inside one broken orbit | 192x192 | Square, uniform-scale safe |
| `orbit_arc` | Wide thin elliptical orbit with two restrained star nodes | 512x256 | 2:1, crop-safe ends |
| `trajectory_line` | Long horizontal comet trajectory with a quiet middle span | 512x64 | 8:1, stretch-safe center |
| `calibration_ticks` | Horizontal cyan calibration baseline with sparse gold ticks | 256x64 | 4:1, artwork reaches both horizontal crop edges |
| `callout_frame` | Angular celestial callout border and corner notches | 512x256 | 2:1, transparent center, final 32 px border |
| `callout_connector` | Thin horizontal angular connector with quiet center span | 256x64 | 4:1, stretch-safe center |
| `catalogue_rail_endcap` | Tall celestial rail cap with compass finial | 128x256 | 1:2, uniform-scale safe |
| `ignition_seal` | Circular ignition sigil with an open center | 192x192 | Square, uniform-scale safe |
| `constellation_corner` | One right-angle constellation corner flourish | 128x128 | Square, outer edges inset |
| `constellation_divider` | Long sparse constellation divider with a central star | 512x64 | 8:1, stretch-safe center |
| `partial_sigil` | Deliberately incomplete circular sigil fragment | 256x256 | Square, uniform-scale safe |
| `focus_halo` | Thin cyan circular halo with four small cardinal points | 96x96 | Square, clearly cyan |
| `selection_halo` | Thin gold circular halo with four offset star points | 96x96 | Square, clearly different geometry from focus |

`callout_frame` additionally requires the source border to be 64 source pixels inside its centered 1024x512 crop so downsampling produces the exact 32 px preservation margin. `focus_halo` and `selection_halo` are node overlays only; retain circular geometry without nonuniform crop or resize. `calibration_ticks` remains vertically inset but reaches both horizontal crop edges for seamless true-height repetition.
