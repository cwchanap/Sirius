# HPA-374 UI Asset Manifest

## Fonts and licensing

HPA-374 ships the five approved font binaries below. Each font is distributed
under its committed SIL Open Font License copy in the same family directory.
Theme wiring and screen migrations are outside HPA-374; this change only
bundles and validates the resources that HPA-373 will wire into the shared
Theme.

### Pinned upstream provenance

| Family | Upstream revision | Direct source URL |
| --- | --- | --- |
| Cinzel | Google Fonts [`7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`](https://github.com/google/fonts/commit/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26) | <https://raw.githubusercontent.com/google/fonts/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26/ofl/cinzel/Cinzel%5Bwght%5D.ttf> |
| Cinzel license | Google Fonts [`7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`](https://github.com/google/fonts/commit/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26) | <https://raw.githubusercontent.com/google/fonts/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26/ofl/cinzel/OFL.txt> |
| Noto Sans | archived official notofonts/noto-fonts [`ffebf8c1ee449e544955a7e813c54f9b73848eac`](https://github.com/notofonts/noto-fonts/commit/ffebf8c1ee449e544955a7e813c54f9b73848eac) | <https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Regular.ttf> |
| Noto Sans | archived official notofonts/noto-fonts [`ffebf8c1ee449e544955a7e813c54f9b73848eac`](https://github.com/notofonts/noto-fonts/commit/ffebf8c1ee449e544955a7e813c54f9b73848eac) | <https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Medium.ttf> |
| Noto Sans | archived official notofonts/noto-fonts [`ffebf8c1ee449e544955a7e813c54f9b73848eac`](https://github.com/notofonts/noto-fonts/commit/ffebf8c1ee449e544955a7e813c54f9b73848eac) | <https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-SemiBold.ttf> |
| Noto Sans Mono | archived official notofonts/noto-fonts [`ffebf8c1ee449e544955a7e813c54f9b73848eac`](https://github.com/notofonts/noto-fonts/commit/ffebf8c1ee449e544955a7e813c54f9b73848eac) | <https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSansMono/NotoSansMono-Medium.ttf> |
| Noto Sans and Noto Sans Mono licenses | archived official notofonts/noto-fonts [`ffebf8c1ee449e544955a7e813c54f9b73848eac`](https://github.com/notofonts/noto-fonts/commit/ffebf8c1ee449e544955a7e813c54f9b73848eac) | <https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/LICENSE> |

### Runtime roles and verified files

| Runtime file | Family, style, and weight | Sirius role | SHA-256 |
| --- | --- | --- | --- |
| `assets/fonts/cinzel/Cinzel-Variable.ttf` | Cinzel variable roman; weight axis used at SemiBold 600 | Cinzel SemiBold display role using the variable font's weight axis at 600 | `f4d83d34d1f6c741193e4acf4b3dff9531e5a67b6aa65228d00a7db72a4e0f34` |
| `assets/fonts/cinzel/OFL.txt` | SIL Open Font License | Cinzel license copy | `f2b3029aba64c378bf0963b62945eee15e564fe4330b934c8f2eb058282b5e83` |
| `assets/fonts/noto_sans/NotoSans-Regular.ttf` | Noto Sans Regular, 400 | Body text | `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5` |
| `assets/fonts/noto_sans/NotoSans-Medium.ttf` | Noto Sans Medium, 500 | Controls and compact labels | `7bbe267354704c6ad18bde24b1dbc756c8e4380ca1c3f3c25c45ec5c4471510b` |
| `assets/fonts/noto_sans/NotoSans-SemiBold.ttf` | Noto Sans SemiBold, 600 | Emphasis and headings | `87a8b90ece1e89746b544e4e086f85a3710e41485a8078f9be874837dfad45d5` |
| `assets/fonts/noto_sans/OFL.txt` | SIL Open Font License | Noto Sans license copy | `0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb` |
| `assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf` | Noto Sans Mono Medium, 500 | Numeric/stat readouts and input-overlay labels | `b1e09ba9f3607d81aedc9e4e1cbe225a0df85c77bde267931a1ab28577840edd` |
| `assets/fonts/noto_sans_mono/OFL.txt` | SIL Open Font License | Noto Sans Mono license copy | `0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb` |

The runtime regression test resolves each `.ttf` through Godot's
`ResourceLoader` and requires a non-null `FontFile`; no system-font fallback
or filename substitution is accepted. Font `.import` sidecars are generated
locally by Godot and are not committed.
