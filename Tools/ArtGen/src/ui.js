// uGUI sprites, drawn as 9-slice sources: every corner and edge detail lies inside SLICE_BORDER pixels of the edge,
// and the middle is one flat colour, so Unity can stretch the centre to any size without visible distortion.
'use strict';

const C = require('./palette');
const { svg, rect, circle } = require('./svg');

/** Canvas size of every UI sprite, in pixels. */
const SIZE = 64;

/** 9-slice border on all four sides, in pixels. Must match GeneratedArtImporter.UiSliceBorder in Unity. */
const SLICE_BORDER = 16;

/** Side panel background: dark steel plate with a raised edge and corner rivets. */
function panel() {
  const rivets = [[9, 9], [SIZE - 9, 9], [9, SIZE - 9], [SIZE - 9, SIZE - 9]]
    .map(([x, y]) => circle(x, y, 2, C.uiPanelLight)).join('');
  return svg(SIZE, SIZE, [
    rect(1, 1, SIZE - 2, SIZE - 2, C.uiPanel, `rx="10" stroke="${C.uiEdge}" stroke-width="2"`),
    rect(4, 4, SIZE - 8, SIZE - 8, 'none', `rx="7" stroke="${C.uiPanelLight}" stroke-width="1.5"`),
    rivets,
  ].join('\n'));
}

/** Production menu row: lighter plate with a red faction stripe on the left edge. */
function row() {
  return svg(SIZE, SIZE, [
    rect(1, 1, SIZE - 2, SIZE - 2, C.uiRow, `rx="8" stroke="${C.uiEdge}" stroke-width="2"`),
    rect(3, 3, SIZE - 6, 2, C.uiRowLight, 'rx="1"'),
    rect(3, 6, 4, SIZE - 12, C.red, 'rx="1.5"'),
  ].join('\n'));
}

/** Production button in the information panel: raised plate with a darker bottom bevel. */
function button() {
  return svg(SIZE, SIZE, [
    rect(1, 1, SIZE - 2, SIZE - 2, C.uiButtonShade, `rx="8" stroke="${C.uiEdge}" stroke-width="2"`),
    rect(2, 2, SIZE - 4, SIZE - 8, C.uiButton, 'rx="7"'),
    rect(4, 3, SIZE - 8, 2, C.uiButtonLight, 'rx="1"'),
  ].join('\n'));
}

module.exports = {
  SIZE,
  SLICE_BORDER,
  sprites: [
    { name: 'Panel', size: [SIZE, SIZE], build: panel },
    { name: 'Row', size: [SIZE, SIZE], build: row },
    { name: 'Button', size: [SIZE, SIZE], build: button },
  ],
};
