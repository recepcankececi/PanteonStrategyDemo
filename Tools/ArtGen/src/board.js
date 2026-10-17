// Board sprites that are not objects: the ground tile, the board's closing edge, the white cell used for highlights,
// the health bar strip, the Barracks spawn marker and the combat effects (tracer, destruction burst).
'use strict';

const C = require('./palette');
const { CELL, svg, rect, circle, outlinedLine } = require('./svg');

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

/**
 * Health bar strip: a plain white bar. Unity draws it twice, a dark tint for the background and a coloured tint for the
 * remaining health scaled along X (HealthBar.cs), so one sprite serves both and every bar batches with the world atlas.
 * Square corners: the fill is scaled horizontally, which would squash rounded ones.
 */
function healthBar() {
  return svg(HEALTH_BAR_SIZE[0], HEALTH_BAR_SIZE[1], rect(0, 0, HEALTH_BAR_SIZE[0], HEALTH_BAR_SIZE[1], '#ffffff'));
}

/**
 * Tracer: a white streak, opaque in the middle and fading at both ends. Unity stretches it along X from the
 * shooter to the target and tints and fades it with SpriteRenderer.color (CombatEffects.cs), so it stays one atlas
 * sprite on the shared material. Ends fade so the stretched streak has no hard edge.
 */
function tracer() {
  const [w, h] = TRACER_SIZE;
  return svg(w, h, [
    '<defs><linearGradient id="fade" x1="0" y1="0" x2="1" y2="0">',
    '<stop offset="0" stop-color="#ffffff" stop-opacity="0"/>',
    '<stop offset="0.3" stop-color="#ffffff" stop-opacity="1"/>',
    '<stop offset="0.7" stop-color="#ffffff" stop-opacity="1"/>',
    '<stop offset="1" stop-color="#ffffff" stop-opacity="0"/>',
    '</linearGradient></defs>',
    rect(0, 0, w, h, 'url(#fade)', `rx="${h / 2}"`),
  ].join('\n'));
}


/**
 * Barracks spawn marker: a concrete pad with hazard-yellow corner brackets and a yellow ring, drawn on the ground under
 * the cell where produced soldiers appear. Symmetric on purpose: the same sprite serves every side of the building.
 * Inset from the cell edge so neighbouring cells keep their grid line.
 */
function spawnMarker() {
  const inset = 6;
  const arm = 12;
  const far = CELL - inset;
  const brackets = [
    `M${inset} ${inset + arm} V${inset} H${inset + arm}`,
    `M${far - arm} ${inset} H${far} V${inset + arm}`,
    `M${far} ${far - arm} V${far} H${far - arm}`,
    `M${inset + arm} ${far} H${inset} V${far - arm}`,
  ].map((d) => outlinedLine(d, C.yellow, 4));
  const c = CELL / 2;
  return svg(CELL, CELL, [
    rect(inset, inset, CELL - 2 * inset, CELL - 2 * inset, C.padDark, 'rx="6" opacity="0.55"'),
    ...brackets,
    circle(c, c, 11, 'none', `stroke="${C.outline}" stroke-width="6.5"`),
    circle(c, c, 11, 'none', `stroke="${C.yellow}" stroke-width="4"`),
    circle(c, c, 4, C.yellow, `stroke="${C.outline}" stroke-width="1.5"`),
  ].join('\n'));
}
/** Points of a star polygon centred on the canvas: alternating outer and inner radius, first spike pointing up. */
function starPoints(spikes, outer, inner, turn) {
  const points = [];
  for (let i = 0; i < spikes * 2; i++) {
    const radius = i % 2 === 0 ? outer : inner;
    const angle = (Math.PI * i) / spikes + turn - Math.PI / 2;
    points.push(`${(CELL / 2 + radius * Math.cos(angle)).toFixed(2)},${(CELL / 2 + radius * Math.sin(angle)).toFixed(2)}`);
  }
  return points.join(' ');
}

/**
 * Destruction burst: a smoke ring behind two flame stars and a bright core, in the faction palette. Unity grows it and
 * fades it out over a fraction of a second where a building or soldier was destroyed, scaled to the footprint.
 */
function deathBurst() {
  const c = CELL / 2;
  return svg(CELL, CELL, [
    circle(c, c, 29, C.padDark, 'opacity="0.55"'),
    circle(c, c, 22, C.pad, 'opacity="0.6"'),
    `<polygon points="${starPoints(8, 27, 14, 0)}" fill="${C.redLight}"/>`,
    `<polygon points="${starPoints(8, 19, 9, Math.PI / 8)}" fill="${C.yellow}"/>`,
    circle(c, c, 7, C.glow),
    circle(c, c, 3, '#ffffff'),
  ].join('\n'));
}

// Pixel size of the tracer: long enough for a smooth fade, as thin as a bullet trail; stretched in Unity.
const TRACER_SIZE = [32, 4];

// Pixel size of the health bar: three quarters of a cell wide (a soldier's bar stays inside its cell), a few pixels tall.
const HEALTH_BAR_SIZE = [48, 6];

module.exports = [
  { name: 'Ground', cells: [1, 1], build: ground },
  { name: 'CellHighlight', cells: [1, 1], build: cellHighlight },
  { name: 'BoardEdge', cells: [1, 1], build: boardEdge },
  { name: 'HealthBar', size: HEALTH_BAR_SIZE, build: healthBar },
  { name: 'SpawnMarker', cells: [1, 1], build: spawnMarker },
  { name: 'Tracer', size: TRACER_SIZE, build: tracer },
  { name: 'DeathBurst', cells: [1, 1], build: deathBurst },
];
