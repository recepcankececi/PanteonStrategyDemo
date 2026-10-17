// Board sprites that are not objects: the ground tile, the board's closing edge and the white cell used for highlights.
'use strict';

const C = require('./palette');
const { CELL, svg, rect } = require('./svg');

/**
 * Seamless one-cell field tile. Unity tiles it over the whole board, so the faint line on its top and left
 * edges becomes the grid. Tufts are placed deterministically: regenerating gives the same pixels.
 */
function ground() {
  const tufts = [[9, 14], [40, 9], [25, 31], [52, 38], [13, 50], [44, 56], [31, 59]]
    .map(([x, y], i) => `<path d="M${x} ${y} l2 -5 l2 5 l2 -4 l1 4" fill="none" stroke="${i % 2 ? C.grassDark : C.grassLight}" stroke-width="1.5" stroke-linecap="round"/>`)
    .join('');
  return svg(CELL, CELL, [
    rect(0, 0, CELL, CELL, C.grass),
    rect(20, 18, 6, 4, C.grassDark, 'rx="2"'),
    rect(46, 44, 5, 3, C.grassLight, 'rx="1.5"'),
    tufts,
    // Grid line: one pixel on two edges only, so neighbouring tiles never draw it twice.
    rect(0, 0, CELL, 1, '#000', 'opacity="0.14"'),
    rect(0, 1, 1, CELL - 1, '#000', 'opacity="0.14"'),
  ].join('\n'));
}

/**
 * The grid line the ground tiles leave out: the board's right and bottom edges (each tile draws only its top and left
 * lines). Unity draws it 9-sliced over the whole board with a 2 px border (the line and one clear pixel inside it,
 * BoardEdgeSliceBorder in GeneratedArtImporter), so the line stays 1 px at any board size and matches the tile's line
 * exactly; the transparent centre stretches without sampling the line.
 */
function boardEdge() {
  return svg(CELL, CELL, [
    rect(CELL - 1, 0, 1, CELL, '#000', 'opacity="0.14"'),
    rect(0, CELL - 1, CELL - 1, 1, '#000', 'opacity="0.14"'),
  ].join('\n'));
}

/**
 * White cell: 2 px opaque frame and a semi-transparent fill. White so a SpriteRenderer.color tint shows its true
 * colour (green/red placement preview, yellow selection); tiled once per cell under a footprint.
 */
function cellHighlight() {
  return svg(CELL, CELL, [
    rect(2, 2, CELL - 4, CELL - 4, '#ffffff', 'opacity="0.55"'),
    `<path d="M0 0 H${CELL} V${CELL} H0 Z M2 2 V${CELL - 2} H${CELL - 2} V2 Z" fill="#ffffff" fill-rule="evenodd"/>`,
  ].join('\n'));
}

module.exports = [
  { name: 'Ground', cells: [1, 1], build: ground },
  { name: 'CellHighlight', cells: [1, 1], build: cellHighlight },
  { name: 'BoardEdge', cells: [1, 1], build: boardEdge },
];
