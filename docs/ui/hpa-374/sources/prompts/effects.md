Use case: stylized-concept
Asset type: Sirius RPG reusable static battle UI effect master
Input images: HPA-373 battle-preparation artwork and retained battle background, used only for Sirius palette, celestial line language, and anime-fantasy rendering.
Referenced image paths (resolve `<repo-root>` to the Sirius checkout root before image generation):

```json
{
  "referenced_image_paths": [
    "<repo-root>/docs/ui/hpa-373/reference/battle-preparation-reference.png",
    "<repo-root>/assets/sprites/ui/ui_battle_background.png"
  ]
}
```

Scene/backdrop: perfectly flat solid `#00FF00` chroma-key background for removal.
Style/medium: crisp mystical anime-fantasy battle effect, luminous celestial-navigation energy, strong dark indigo structural edges, controlled glow that remains separable from the key.
Composition/framing: exactly one centered subject on a square 1024x1024 source canvas with at least 18% clear padding on every edge. Keep the complete subject inside the centered square crop and at least twice the 256x256 runtime resolution.
Color palette: `#050714`, `#0D1530`, `#18234A`, `#27366C`, `#F7F5FF`, `#C7CEE8`, `#8F9AB8`, `#62DCFF`, `#F5D784`, `#DFAE43`, `#D96CC2`, `#F16D83`.
Constraints: no text, no letters, no numbers, no watermark, no scene background, no panel, no cast shadow, no contact shadow, no reflection, and do not use `#00FF00` in the subject. The flat background must contain no gradient, texture, particles, floor plane, or lighting variation. The effect must remain a static, isolated overlay: do not depict a character, weapon, UI control, battle layout, or animation frames.
Avoid: photorealism, generic mobile emoji, glossy app-store tiles, smoke, fog, translucent glass, dense micro-detail, chrome bevels, circuitry, and baked UI labels.

| ID | Subject | Runtime size | Required geometry |
|---|---|---:|---|
| `encounter_burst` | Radial cyan-and-gold celestial gate burst with eight uneven rays and a transparent center | 256x256 | Square, radial, all rays inset |
| `hit_impact` | Sharp rose-and-gold crossed impact slash with a compact white core | 256x256 | Square, crossed diagonal silhouette, inset |
| `status_pulse` | Expanding cyan/violet circular status wave with four orbit nodes and a transparent center | 256x256 | Square, circular, inset |
| `reward_level_up` | Upward gold constellation bloom with one rising central star and two restrained arcs | 256x256 | Square, ascending silhouette, inset |

For every individual call, repeat the common specification above and request exactly the matching table subject only. The source remains on the flat `#00FF00` key until local chroma extraction; do not request native transparency.
