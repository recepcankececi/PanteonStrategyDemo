// Buildings, seen from straight above. Each canvas is exactly the footprint (CELL px per cell), so a building
// cannot draw outside its cells: resvg clips at the canvas edge. Doors face the bottom edge, where units exit.
'use strict';

const C = require('./palette');
const { CELL, svg, rect, circle, stroke, polygonPoints, star } = require('./svg');

/** Concrete foundation filling the whole footprint, with a light top/left bevel and corner bolts. */
function pad(w, h) {
  const bolts = [[10, 10], [w - 10, 10], [10, h - 10], [w - 10, h - 10]]
    .map(([x, y]) => circle(x, y, 3, C.padDark)).join('');
  return [
    rect(1.5, 1.5, w - 3, h - 3, C.pad, `rx="6" ${stroke(3)}`),
    `<path d="M6 ${h - 8} V8 H${w - 8}" fill="none" stroke="${C.padLight}" stroke-width="2"/>`,
    `<path d="M${w - 6} 8 V${h - 6} H8" fill="none" stroke="${C.padDark}" stroke-width="2"/>`,
    bolts,
  ].join('\n');
}

/** Diagonal yellow/black hazard stripes clipped to a rectangle; ids must be unique within one SVG. */
function hazard(id, x, y, w, h) {
  let stripes = '';
  for (let i = -h; i < w + h; i += 10) {
    stripes += `<path d="M${x + i} ${y + h} L${x + i + h} ${y} L${x + i + h + 5} ${y} L${x + i + 5} ${y + h} Z" fill="${C.hazardDark}"/>`;
  }
  return [
    `<clipPath id="${id}">${rect(x, y, w, h, '#000')}</clipPath>`,
    rect(x, y, w, h, C.yellow),
    `<g clip-path="url(#${id})">${stripes}</g>`,
    rect(x, y, w, h, 'none', stroke(2)),
  ].join('\n');
}

/** Roof vent with horizontal slats. */
function vent(x, y, w, h) {
  let slats = '';
  for (let sy = y + 4; sy < y + h - 2; sy += 4) slats += rect(x + 3, sy, w - 6, 1.5, C.metal);
  return rect(x, y, w, h, C.roofShade, `rx="2" ${stroke(2)}`) + slats;
}

/** Light apron in front of a door, with yellow chevrons pointing out of the building. */
function apron(x, y, w, h) {
  const cx = x + w / 2;
  return [
    rect(x, y, w, h, C.padLight, stroke(2)),
    `<path d="M${cx - 8} ${y + 12} L${cx} ${y + 20} L${cx + 8} ${y + 12} M${cx - 8} ${y + 24} L${cx} ${y + 32} L${cx + 8} ${y + 24}" fill="none" stroke="${C.yellow}" stroke-width="3"/>`,
  ].join('\n');
}

/** Barracks, 3x3: hall with a flat roof, red trim and emblem, guard posts and an apron at the door. */
function barracks() {
  const s = 3 * CELL;
  return svg(s, s, [
    pad(s, s),
    apron(70, 146, 52, 40),
    rect(18, 16, 156, 130, C.wall, `rx="5" ${stroke(3)}`),
    rect(24, 22, 144, 112, C.roof, 'rx="3"'),
    rect(24, 22, 144, 8, C.roofLight, 'rx="3"'),
    rect(24, 122, 144, 12, C.red),
    rect(24, 132, 144, 2, C.redDark),
    vent(34, 36, 30, 22),
    vent(128, 36, 30, 22),
    rect(34, 72, 24, 40, C.roofShade, `rx="2" ${stroke(2)}`),
    rect(134, 72, 24, 40, C.roofShade, `rx="2" ${stroke(2)}`),
    circle(96, 76, 26, C.redDark, stroke(3)),
    circle(96, 76, 21, C.red),
    star(96, 77, 16, C.roofLight),
    rect(78, 128, 36, 20, C.metal, stroke(2)),
    rect(82, 132, 28, 3, C.yellow),
    rect(12, 150, 40, 32, C.wall, `rx="4" ${stroke(3)}`),
    circle(32, 166, 9, C.roofShade, stroke(2)),
    rect(140, 150, 40, 32, C.wall, `rx="4" ${stroke(3)}`),
    circle(160, 166, 9, C.roofShade, stroke(2)),
  ].join('\n'));
}

/** Power Plant, 2x2: generator block with a power emblem, two cooling towers and connecting pipes. */
function powerPlant() {
  const s = 2 * CELL;
  const tower = (cx, cy) => [
    circle(cx, cy, 22, C.wall, stroke(3)),
    circle(cx, cy, 18, C.roof),
    circle(cx, cy, 13, C.metal, stroke(2)),
    circle(cx - 2, cy - 2, 8, C.steam, 'opacity="0.35"'),
    circle(cx - 4, cy - 4, 4, C.steam, 'opacity="0.5"'),
  ].join('\n');
  return svg(s, s, [
    pad(s, s),
    // Pipes first so the block and towers overlap their ends.
    rect(62, 32, 22, 10, C.roofShade, stroke(2)),
    rect(62, 84, 22, 10, C.roofShade, stroke(2)),
    rect(10, 10, 56, 108, C.wall, `rx="4" ${stroke(3)}`),
    rect(15, 15, 46, 88, C.roof, 'rx="2"'),
    rect(15, 15, 46, 6, C.roofLight, 'rx="2"'),
    rect(15, 92, 46, 6, C.red),
    circle(38, 54, 17, C.hazardDark, stroke(2)),
    circle(38, 54, 13, 'none', `stroke="${C.glow}" stroke-width="1.5" opacity="0.6"`),
    `<path d="M41 40 L30 56 H37 L34 68 L46 51 H39 L43 40 Z" fill="${C.glow}"/>`,
    vent(20, 76, 36, 13),
    hazard('powerPlantHazard', 15, 103, 46, 10),
    tower(98, 36),
    tower(98, 90),
  ].join('\n'));
}

/** Command Center, 4x4: headquarters hall with a glass command dome, helipad, fuel tanks and a vehicle bay. */
function commandCenter() {
  const s = 4 * CELL;
  const fuelTank = (cx, cy) => [
    circle(cx, cy, 13, C.roofShade, stroke(2.5)),
    circle(cx, cy, 8, C.roof),
    circle(cx - 3, cy - 3, 3, C.roofLight),
  ].join('\n');
  return svg(s, s, [
    pad(s, s),
    apron(100, 200, 56, 44),
    // Main hall.
    rect(20, 20, 216, 170, C.wall, `rx="6" ${stroke(3)}`),
    rect(26, 26, 204, 152, C.roof, 'rx="3"'),
    rect(26, 26, 204, 8, C.roofLight, 'rx="3"'),
    rect(26, 166, 204, 12, C.red),
    rect(26, 176, 204, 2, C.redDark),
    vent(38, 42, 36, 24),
    vent(182, 42, 36, 24),
    vent(38, 124, 36, 24),
    vent(182, 124, 36, 24),
    // Command dome in the middle of the roof.
    circle(128, 96, 46, C.wall, stroke(3)),
    circle(128, 96, 40, C.roofShade),
    circle(128, 96, 32, C.glass, stroke(2.5)),
    `<path d="M108 84 Q118 70 134 68" fill="none" stroke="${C.glassLight}" stroke-width="4" stroke-linecap="round" opacity="0.7"/>`,
    star(128, 98, 13, C.redLight),
    // Antenna mast on the roof's right side.
    `<path d="M206 88 L222 66" fill="none" stroke="${C.outline}" stroke-width="3" stroke-linecap="round"/>`,
    circle(222, 66, 4, C.red, stroke(1.5)),
    // Vehicle bay door.
    rect(104, 172, 48, 28, C.metal, stroke(2)),
    rect(108, 178, 40, 3, C.yellow),
    rect(108, 186, 40, 3, C.yellow),
    // Helipad, bottom left.
    circle(56, 222, 26, C.padDark, stroke(2.5)),
    circle(56, 222, 20, 'none', `stroke="${C.yellow}" stroke-width="2.5"`),
    `<path d="M48 212 V232 M64 212 V232 M48 222 H64" fill="none" stroke="${C.yellow}" stroke-width="4" stroke-linecap="square"/>`,
    // Fuel tanks, bottom right.
    fuelTank(194, 222),
    fuelTank(226, 222),
  ].join('\n'));
}

/** Supply Depot, 2x1: two shipping containers and a stack of crates on a short pad. */
function supplyDepot() {
  const w = 2 * CELL;
  const h = CELL;
  const container = (x, y, fill, dark) => {
    let ribs = '';
    for (let rx = x + 6; rx < x + 50; rx += 6) ribs += rect(rx, y + 3, 1.5, 14, dark);
    return rect(x, y, 54, 20, fill, `rx="2" ${stroke(2)}`) + ribs;
  };
  const crate = (x, y) => [
    rect(x, y, 20, 20, C.crate, `rx="1.5" ${stroke(2)}`),
    `<path d="M${x + 3} ${y + 3} L${x + 17} ${y + 17} M${x + 17} ${y + 3} L${x + 3} ${y + 17}" stroke="${C.crateDark}" stroke-width="2"/>`,
  ].join('\n');
  return svg(w, h, [
    pad(w, h),
    container(10, 10, C.olive, C.oliveDark),
    container(10, 34, C.red, C.redDark),
    crate(72, 10),
    crate(96, 10),
    crate(72, 34),
    hazard('supplyDepotHazard', 96, 36, 22, 16),
  ].join('\n'));
}

/** Radar, 2x2: a large dish above a small control hut. */
function radar() {
  const s = 2 * CELL;
  return svg(s, s, [
    pad(s, s),
    // Control hut, bottom left.
    rect(10, 72, 50, 46, C.wall, `rx="4" ${stroke(3)}`),
    rect(15, 77, 40, 34, C.roof, 'rx="2"'),
    rect(15, 104, 40, 7, C.red),
    rect(22, 84, 26, 10, C.glass, `rx="2" ${stroke(1.5)}`),
    hazard('radarHazard', 70, 104, 48, 12),
    // Dish: rim, bowl rings, feed struts and the feed horn in the middle.
    circle(76, 52, 40, C.roofLight, stroke(3)),
    circle(73, 49, 31, C.roof, `stroke="${C.roofShade}" stroke-width="2"`),
    circle(71, 47, 20, 'none', `stroke="${C.roofShade}" stroke-width="2"`),
    `<path d="M76 13 L70 46 M41 70 L70 46 M111 70 L70 46" fill="none" stroke="${C.metal}" stroke-width="2.5" stroke-linecap="round"/>`,
    circle(70, 46, 6, C.metal, stroke(1.5)),
    circle(70, 46, 2.5, C.redLight),
  ].join('\n'));
}

/** Guard Tower, 1x1: octagonal tower with a turret whose barrel points to the top edge. */
function guardTower() {
  const s = CELL;
  return svg(s, s, [
    pad(s, s),
    `<polygon points="${polygonPoints(32, 32, 25, 8)}" fill="${C.wall}" ${stroke(3)}/>`,
    `<polygon points="${polygonPoints(32, 32, 19, 8)}" fill="${C.roof}"/>`,
    circle(32, 32, 15, 'none', `stroke="${C.red}" stroke-width="3"`),
    rect(30, 6, 4, 20, C.metal, `rx="1" ${stroke(1.5)}`),
    circle(32, 33, 9, C.metal, stroke(2)),
    circle(29.5, 30.5, 2.5, C.roofShade),
  ].join('\n'));
}

/** Building sprites with their footprint in cells; the canvas is footprint x CELL pixels. */
module.exports = [
  { name: 'Barracks', cells: [3, 3], build: barracks },
  { name: 'PowerPlant', cells: [2, 2], build: powerPlant },
  { name: 'CommandCenter', cells: [4, 4], build: commandCenter },
  { name: 'SupplyDepot', cells: [2, 1], build: supplyDepot },
  { name: 'Radar', cells: [2, 2], build: radar },
  { name: 'GuardTower', cells: [1, 1], build: guardTower },
];
