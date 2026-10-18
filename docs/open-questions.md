# Crayons Studio Remake: open questions

Questions the spec does not settle. Each has a working answer (see `decisions.md`);
verify against the original under otvdm in Phase 6 (with the user's approval to run it).

| # | Question | Current behaviour | Ref |
|---|---|---|---|
| Q1 | Shade click area: shell.md says 180 <= y <= 212, input.md says 181 <= y <= 212 (1-px difference). | input.md (181..212), per spec §8.3. | S10 |
| Q2 | Brush type panel file order: tools-paint.md gives 1→a11, 2→a12, 3→a13, 4→a14; shell.md lists A11, A12, A14, A13. | tools-paint.md order (spec §8.3). | S11 |
| Q3 | Left click on the toolbar beyond the last tab: clamp to the last tab, or nothing? | Nothing. | B3 |
| Q4 | Right click on the toolbar beyond the last tab: the unclamped formula gives caption 141 (Older), which is a Symmetry caption. Did the original really show it? | Unclamped, as documented. | B3 |
| Q5 | Does clicking the already-selected tab fire the tab click (SND93, Mode = 0)? | Yes. | B4 |
| Q6 | Initial content of undo buffer A (UNDO pressed before any snapshot). | White. | B5 |
| Q7 | Exact X-marker diagonal end points in the shade grid. | (l,t)→(r,b), (r-1,t)→(l-1,b). | B6 |
| Q8 | Options-row timer: blit then advance, or advance then blit? Is anything drawn at (100,10) before the first tick? | Blit then advance; nothing before the first tick. | B7 |
| Q9 | Where exactly is Caterpillar's extra clown-mode `Random(16)` call within the burst? | At the start of the burst. | B9 |
| Q10 | Are band tooltips gated by panel visibility (including band D's 88/115 for the colour mode panel)? | Yes. | B11 |
| Q11 | Do ABCD options 1-4 show the thickness panel (the table lists it only for option 0)? | Yes, all five. | B13 |
| Q12 | Toolbar icon clip: the right bound (123 + 41i) and bottom (y < 91) are observed; the left bound (82 + 41i) is inferred. | As stated. | B2 |
| Q13 | Win16 wide-pen rendering (round caps, zero-length segments, 2-px pens) versus modern GDI. | Modern GDI as is. Zero-length segments (Chalk and Thick/Thin press) draw whatever GDI draws. | spec §3.1 |
| Q14 | Mouse event density: is 20 ms coalescing close enough for Thick/Thin and Chalk on the original? | 20 ms. | S4 |
| Q15 | Whether CRAYON1 ever draws the SO panel. | Not drawn (spec §8.9). | S18 |
| Q16 | Original message-box metrics (button size, text position) under Windows 95. | Approximate Win95 layout. | B19 |
| Q17 | The tooltip panel's label height for an empty piece of a caption ("a\|\|b"). | Height of " ". | B10 |
| Q18 | How Win16 draws a zero-length width-2 `LineTo` (big spray at t = 2). | 2x2 square; widths >= 3 use GDI's own round dot. | B25 |
| Q19 | What the bucket does with a style >= 16 picked on its dialog's last visible row (form y 347). | Selectable; the fill paints nothing (null brush). | B27 |
| Q20 | Can the FillStyles dialog be dismissed without a pick (Esc, click outside)? | No. | B28 |
| Q21 | Exact drawing calls of the paint-can preview (fill per style, brush origin, outline). | Clip to the polygon; PatBlt / gradient / PB blit over the 54x54 box at (15,299); no outline. | B30 |
| Q22 | Are the diamond/hexagon/octagon/star midpoints "toward X" on right-to-left drags? | Yes (signed differences). | B31 |
| Q23 | Preview outline of sphere / box / emerald. | Their own primitive over the pass-0 box. | B32 |
| Q24 | Parallelogram: does the press in step 2 reset the anchor? What do zero-size releases do in either step? | Anchor kept; zero-size releases neither advance nor commit. | B33 |
| Q25 | Jumble and smear random ranges and call order. | As in B35/B36. | B35 |
| Q26 | Train drift mapping 1→1, 2→0, 3→1 (never upward): is 3 really +1, not -1? | As documented. | B39 |
| Q27 | Animation frame interval: Win16 tick granularity gave about 110 ms; the remake uses > 100 ms (about 105 ms). | > 100 ms. | B37 |
| Q28 | Did the options-row animation (Timer1) pause during the Mr. Brush/Train busy loops? | It keeps running. | B37 |
| Q29 | Undo timing for paint/shape strokes. **Answered by the lead (content-files.md §8):** strokes never update A; snapshots happen only at the documented events. | Closed. | B67 |
| Q30 | Rubber band primitive for the box tools: closed polyline or Rectangle with a null brush (the extents differ by 1 px)? | Polyline. | B45 |
| Q31 | Real speed of the eraser, tunnel and squiggle effects on the original (busy loops with no documented delay). | Pacing in B52. | B52 |
| Q32 | Crumple: is step 1's whitening clipped (there is no previous region), and are the 51 points drawn x then y inside the inset? | Unclipped; x then y. | B54 |
| Q33 | Face circles: where does each row's x start? | left + 3, like the squares. | B56 |
| Q34 | Zoom out: is the "whole snapshot" buffer A or the current canvas? | A copy of the current canvas. | B56 |
| Q35 | Clip-art stamp: "snapshot into A on mouse-down" contradicts "Undo removes the stamps". | Snapshot on every press, so Undo removes the latest stamp only. | B58 |
| Q36 | Icons and captions of the save, trash and no-printer boxes. | As in B61. | B61 |
| Q37 | Hover-help texts of TextInpt, FontForm and Rotator (their Timer2 + Panel1). | Not built. | B68 |
| Q38 | Size of a blank cell in the clip-art browser (orientation is recorded only for loaded files). | 200x100 (H). | B59 |
| Q39 | Does ABCD append control characters other than Backspace and Enter (Tab, Esc)? | Ignored. | B51 |
| Q40 | Does the About box's caption close button (BorderIcons biSystemMenu) close it, contradicting "closes only on the bottom-centre area"? | No close button drawn. | B62 |
| Q41 | Which dimension the print bitmap's "-5" applies to. | Not applied. | B61 |
| Q42 | Does the rotated-text option (0x4F) fire on every release, even after a drag? | Every release. | - |
| Q43 | Does the Older text box need X and Y both changed before TextInpt opens (the shared box rule)? | Yes. | B45 |

## Answered by the Phase 6 runtime comparison (spec §9)

Q1 (180..212, B83), Q5 (no-op, B82), Q7 (B78), Q8 (B84), Q27 (B85), Q31 (B86), Q40 (B77) are answered and implemented. New open point: is the sound of the About caption X SND92 or nothing? The remake plays nothing (B77).