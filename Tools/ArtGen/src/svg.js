// Small SVG string helpers shared by every sprite module. All coordinates are output pixels.
'use strict';

/** Pixels per grid cell. Must match GridConfig.CellPixelSize and the board sprites' Pixels Per Unit in Unity. */
const CELL = 64;

const C = require('./palette');

/** Wraps a body in an <svg> whose canvas is exactly width x height pixels. Anything outside is clipped. */
const svg = (width, height, body) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">\n${body}\n</svg>\n`;

const rect = (x, y, w, h, fill, extra = '') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${fill}" ${extra}/>`;

const circle = (cx, cy, r, fill, extra = '') => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" ${extra}/>`;

const ellipse = (cx, cy, rx, ry, fill, extra = '') => `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="${fill}" ${extra}/>`;

/** The dark outline every shape uses, so all sprites share one line weight family. */
const stroke = (width = 3) => `stroke="${C.outline}" stroke-width="${width}" stroke-linejoin="round"`;

/** A rounded stroke-only line, drawn twice (outline under colour) so it reads at gameplay zoom. */
function outlinedLine(d, colour, width) {
  return [
    `<path d="${d}" fill="none" stroke="${C.outline}" stroke-width="${width + 2.5}" stroke-linecap="round"/>`,
    `<path d="${d}" fill="none" stroke="${colour}" stroke-width="${width}" stroke-linecap="round"/>`,
  ].join('\n');
}

/** Regular polygon points (e.g. an octagon) around a centre; the first corner sits at angle offset. */
function polygonPoints(cx, cy, r, sides, offset = Math.PI / sides) {
  const points = [];
  for (let i = 0; i < sides; i++) {
    const angle = offset + (i * 2 * Math.PI) / sides;
    points.push(`${(cx + r * Math.cos(angle)).toFixed(1)},${(cy + r * Math.sin(angle)).toFixed(1)}`);
  }
  return points.join(' ');
}

/** Five-pointed star, the faction emblem. */
function star(cx, cy, r, fill) {
  const points = [];
  for (let i = 0; i < 10; i++) {
    const radius = i % 2 === 0 ? r : r * 0.42;
    const angle = -Math.PI / 2 + (i * Math.PI) / 5;
    points.push(`${(cx + radius * Math.cos(angle)).toFixed(1)},${(cy + radius * Math.sin(angle)).toFixed(1)}`);
  }
  return `<polygon points="${points.join(' ')}" fill="${fill}"/>`;
}

module.exports = { CELL, svg, rect, circle, ellipse, stroke, outlinedLine, polygonPoints, star };
