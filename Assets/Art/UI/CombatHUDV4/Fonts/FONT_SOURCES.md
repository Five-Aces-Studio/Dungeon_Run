# Combat HUD V4 fonts

| Role | File | Derived from |
|---|---|---|
| DISPLAY (titles, name, floor, Commit, terminal) | `AlegreyaSCDR-Bold.ttf`, `AlegreyaSCDR-ExtraBold.ttf` | Alegreya SC Bold / ExtraBold |
| BODY (effects, HP, counts, hints) | `AlegreyaSansDR-Regular.ttf`, `-Medium`, `-Bold`, `-ExtraBold` | Alegreya Sans Regular / Medium / Bold / ExtraBold |

- License: SIL Open Font License 1.1 (`OFL_AlegreyaSC.txt`, `OFL_AlegreyaSans.txt`). No Reserved Font Name is declared; the derived files still use a distinct family name (`Alegreya SC DR`, `Alegreya Sans DR`).
- Upstream: https://github.com/google/fonts/tree/main/ofl/alegreyasc and https://github.com/google/fonts/tree/main/ofl/alegreyasans (downloaded 2026-09-24 with the user's authorization).
- Modification: the default digit code points are remapped to the fonts' own tabular lining glyphs (`zero.tf` ... `nine.tf`), because TextMesh Pro cannot apply the `lnum`/`tnum` OpenType features and the default old-style figures made "FLOOR 01" read as "FLOOR o1". The glyph outlines are unchanged.
- Reproduce: `python Tools/CombatHUDV4Fonts/make_lining_fonts.py <upstream ttf folder> Assets/Art/UI/CombatHUDV4/Fonts` (requires fontTools).

Upstream SHA-256:

| File | SHA-256 |
|---|---|
| AlegreyaSC-Bold.ttf | 3ef96cbf3be7b84d9dff9b1aa1e2ac40eec0cf323ccd0638395cdef9c5a91528 |
| AlegreyaSC-ExtraBold.ttf | d485e1fea222301f8600daa70be44027f39795127c9c5fb77a6bd9f5aba8dbf7 |
| AlegreyaSans-Regular.ttf | 8fab634196007afca839f1e5a6fb300976daff55d8528b590ef032f01b14ea10 |
| AlegreyaSans-Medium.ttf | 4b89fe7804fd1485ec2757795a53ffdb66e1206dd56f844c2d72b3c944815b43 |
| AlegreyaSans-Bold.ttf | a3055a1893759bdbd7504bb22abc583769e7974c49353176eac0b03792c9fb8e |
| AlegreyaSans-ExtraBold.ttf | 3861f08a77d5c1b3b311055913443b07337a4b7abaff21a8cdb51fc0ae980b9e |
