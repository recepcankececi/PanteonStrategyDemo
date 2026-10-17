// Soldiers, seen from straight above and facing the top edge (Unity rotates them to their heading).
// Every soldier is drawn inside the circle inscribed in its 64 px cell, so it stays inside the cell at any
// rotation. Each type has a standing pose and a two-frame walk cycle: the boots step forward in turn and the
// upper body sways slightly with them.
'use strict';

const C = require('./palette');
const { CELL, svg, rect, circle, ellipse, stroke, outlinedLine } = require('./svg');

/** Radius around the cell centre the art must stay inside (the inscribed circle is CELL / 2). */
const SAFE_RADIUS = 30;

// Distance a boot moves forward or back per step, in pixels, and the matching body sway in degrees.
const STRIDE = 7;
const SWAY = 4;

/**
 * Draws one soldier pose.
 * @param {object} look  body colours, helmet and weapon of the soldier type
 * @param {number} step  0 = standing (boots together), +1 = left boot forward, -1 = right boot forward
 */
function soldier(look, step) {
  const leftBootY = 42 - step * STRIDE;
  const rightBootY = 42 + step * STRIDE;
  const upperBody = [
    // Backpack sits behind the shoulders; the soldier faces up, so "behind" is down.
    rect(24, 37, 16, 10, look.pack, `rx="3" ${stroke(2)}`),
    ellipse(32, 33, look.shoulders, 8, look.body, stroke(2.5)),
    look.extra || '',
    look.weapon,
    // Arms reach from the shoulders to the weapon grip.
    outlinedLine(`M${32 - look.shoulders + 3} 31 Q28 25 37 23`, look.body, 3.5),
    outlinedLine(`M${32 + look.shoulders - 2} 32 Q43 29 41 29`, look.body, 3.5),
    circle(38, 23, 2.6, C.skin, stroke(1.2)),
    circle(40, 29, 2.6, C.skin, stroke(1.2)),
    look.head,
  ].join('\n');
  return svg(CELL, CELL, [
    ellipse(32, 34, 15, 13, '#000', 'opacity="0.22"'),
    ellipse(26, leftBootY, 3.5, 5, C.metal, stroke(1.5)),
    ellipse(38, rightBootY, 3.5, 5, C.metal, stroke(1.5)),
    `<g transform="rotate(${step * SWAY} 32 33)">\n${upperBody}\n</g>`,
  ].join('\n'));
}

/** Red combat helmet with a highlight toward the light (top left). */
const helmet = [
  circle(31, 31, 8, C.red, stroke(2.5)),
  circle(31, 31, 4.5, C.redDark, 'opacity="0.35"'),
  circle(28.5, 28.5, 2.2, C.redLight),
].join('\n');

const looks = {
  // Assault (Soldier 1): heavy armour, shoulder plates and a short, thick close-range gun.
  Assault: {
    body: C.steel,
    pack: C.steelDark,
    shoulders: 15,
    extra: [circle(19, 32, 4.5, C.steelDark, stroke(2)), circle(45, 32, 4.5, C.steelDark, stroke(2))].join('\n'),
    weapon: [rect(35.5, 10, 6, 24, C.metal, `rx="1.5" ${stroke(1.5)}`), rect(35, 18, 7, 5, C.hazardDark, 'rx="1"')].join('\n'),
    head: [helmet, rect(27, 24.5, 8, 2.5, C.metal, 'rx="1"')].join('\n'),
  },
  // Rifleman (Soldier 2): standard infantry with a long rifle.
  Rifleman: {
    body: C.olive,
    pack: C.oliveDark,
    shoulders: 14,
    weapon: [rect(37, 6, 4, 30, C.metal, `rx="1" ${stroke(1.5)}`), rect(36, 22, 6, 8, C.hazardDark, 'rx="1"')].join('\n'),
    head: helmet,
  },
  // Scout (Soldier 3): light kit, beret and a scoped long-range rifle.
  Scout: {
    body: C.khaki,
    pack: C.khakiDark,
    shoulders: 12,
    weapon: [rect(37.5, 6, 3, 30,C.metal, `rx="1" ${stroke(1.2)}`), rect(36, 16, 6, 7, C.hazardDark, `rx="1.5" ${stroke(1)}`)].join('\n'),
    head: [
      circle(31, 31, 7, C.skin, stroke(2.2)),
      `<path d="M24 31 A7 7 0 0 1 38 31 Q31 27 24 31 Z" fill="${C.red}" ${stroke(1.5)}/>`,
      circle(29, 27, 2, C.redLight),
    ].join('\n'),
  },
};

/** Unit sprites: <Type>_Stand plus <Type>_Walk_0 and <Type>_Walk_1, each one cell. */
module.exports = {
  SAFE_RADIUS,
  sprites: Object.entries(looks).flatMap(([type, look]) => [
    { name: `${type}_Stand`, cells: [1, 1], build: () => soldier(look, 0) },
    { name: `${type}_Walk_0`, cells: [1, 1], build: () => soldier(look, 1) },
    { name: `${type}_Walk_1`, cells: [1, 1], build: () => soldier(look, -1) },
  ]),
};
