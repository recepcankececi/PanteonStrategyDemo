#!/usr/bin/env node
// Generates every sprite of the game from code. Usage:
//   node generate.js            -> SVG + PNG into out/<group>/
//   node generate.js --install  -> also copy the PNGs into Assets/_Project/Art/Generated/<group>/
// Unity imports the copied PNGs with GeneratedArtImporter (pixels per unit, pivot, 9-slice border).
'use strict';

const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');
const { CELL } = require('./src/svg');
const buildings = require('./src/buildings');
const units = require('./src/units');
const board = require('./src/board');
const ui = require('./src/ui');

const install = process.argv.includes('--install');
const outRoot = path.join(__dirname, 'out');
const assetsRoot = path.resolve(__dirname, '../../Assets/_Project/Art/Generated');

// Folder per group; board sprites are sized in cells, UI sprites in pixels.
const groups = [
  { folder: 'Buildings', sprites: buildings },
  { folder: 'Units', sprites: units.sprites, safeRadius: units.SAFE_RADIUS },
  { folder: 'Board', sprites: board },
  { folder: 'UI', sprites: ui.sprites },
];

function expectedSize(sprite) {
  return sprite.cells ? [sprite.cells[0] * CELL, sprite.cells[1] * CELL] : sprite.size;
}

/** Throws if any visible pixel lies farther than radius from the canvas centre (units must survive rotation). */
function assertInsideCircle(name, image, radius) {
  const cx = image.width / 2;
  const cy = image.height / 2;
  for (let y = 0; y < image.height; y++) {
    for (let x = 0; x < image.width; x++) {
      const alpha = image.pixels[(y * image.width + x) * 4 + 3];
      if (alpha > 0 && Math.hypot(x + 0.5 - cx, y + 0.5 - cy) > radius) {
        throw new Error(`${name}: pixel (${x}, ${y}) is outside the ${radius} px safe circle.`);
      }
    }
  }
}

fs.rmSync(outRoot, { recursive: true, force: true });

for (const group of groups) {
  for (const sprite of group.sprites) {
    const svgText = sprite.build();
    // fitTo 'original' keeps the SVG's own size: 1 SVG unit = 1 output pixel.
    const image = new Resvg(svgText, { fitTo: { mode: 'original' }, background: 'rgba(0,0,0,0)' }).render();
    const [width, height] = expectedSize(sprite);
    // The footprint invariant: a building's image is exactly its cells, never larger.
    if (image.width !== width || image.height !== height) {
      throw new Error(`${sprite.name}: rendered ${image.width}x${image.height}, expected ${width}x${height}.`);
    }
    if (group.safeRadius) {
      assertInsideCircle(sprite.name, image, group.safeRadius);
    }

    const png = image.asPng();
    for (const [root, sub] of [[outRoot, 'svg'], [outRoot, 'png']]) {
      fs.mkdirSync(path.join(root, group.folder, sub), { recursive: true });
    }
    fs.writeFileSync(path.join(outRoot, group.folder, 'svg', `${sprite.name}.svg`), svgText);
    fs.writeFileSync(path.join(outRoot, group.folder, 'png', `${sprite.name}.png`), png);
    if (install) {
      const target = path.join(assetsRoot, group.folder);
      fs.mkdirSync(target, { recursive: true });
      fs.writeFileSync(path.join(target, `${sprite.name}.png`), png);
    }
    console.log(`${group.folder}/${sprite.name}.png ${width}x${height}`);
  }

  // A sprite renamed or removed here leaves its old PNG behind in Assets/. Report it; deleting is Unity's job
  // (its .meta must go with it), so this script never removes files from Assets/.
  if (install) {
    const produced = new Set(group.sprites.map((sprite) => `${sprite.name}.png`));
    const orphans = fs.readdirSync(path.join(assetsRoot, group.folder))
      .filter((file) => file.endsWith('.png') && !produced.has(file));
    for (const orphan of orphans) {
      console.warn(`WARNING ${group.folder}/${orphan} is no longer generated; delete it in Unity.`);
    }
  }
}
