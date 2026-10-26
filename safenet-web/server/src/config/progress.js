// Shared mission rules used by the Unity API and professor dashboard.
const GRADED_MISSION_KEYS = [
  'area1',
  'area2',
  'area3',
  'area4',
  'area5',
  'area6',
];

const TOTAL_GRADED_MISSIONS = GRADED_MISSION_KEYS.length;
const MAX_MISSION_SCORE = 1000;
const MAX_GAME_SCORE = 155;

module.exports = {
  GRADED_MISSION_KEYS,
  MAX_GAME_SCORE,
  MAX_MISSION_SCORE,
  TOTAL_GRADED_MISSIONS,
};
