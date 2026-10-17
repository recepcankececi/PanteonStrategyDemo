// One palette for the whole game: concrete greys, a strong red faction colour, hazard yellow, muted field green.
// Every sprite module reads colours from here, which is what keeps buildings, soldiers, ground and UI one style.
'use strict';

module.exports = {
  outline: '#1d2126',

  // Concrete foundations and walls.
  padDark: '#5f656b',
  pad: '#80868c',
  padLight: '#a3a9ae',
  wall: '#8d949a',
  roof: '#c3c8cc',
  roofShade: '#a9afb4',
  roofLight: '#dde1e4',
  metal: '#3a3f44',

  // Faction colour and accents.
  red: '#c8302c',
  redDark: '#8e1f1c',
  redLight: '#e8594d',
  yellow: '#f2c230',
  hazardDark: '#2b2b2b',
  glow: '#ffe27a',
  glass: '#2f4f63',
  glassLight: '#6fd3ff',
  steam: '#ffffff',

  // Soldiers.
  olive: '#5f6b3a',
  oliveDark: '#434c28',
  steel: '#4c5762',
  steelDark: '#343c45',
  khaki: '#9c8a5a',
  khakiDark: '#736541',
  skin: '#d9a47a',

  // Cargo.
  crate: '#b08850',
  crateDark: '#7a5a32',

  // Ground.
  grass: '#5e8240',
  grassDark: '#52733a',
  grassLight: '#6c9249',

  // UI.
  uiPanel: '#2b3138',
  uiPanelLight: '#3d454d',
  uiRow: '#3a424a',
  uiRowLight: '#4b545d',
  uiButton: '#4a535c',
  uiButtonLight: '#5c666f',
  uiButtonShade: '#30373e',
  uiEdge: '#14181c',
};
