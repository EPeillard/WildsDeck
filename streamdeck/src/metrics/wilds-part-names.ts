// Part order follows HunterPie's Wilds MonsterData.xml. Duplicate anatomical
// names are intentional: Wilds can expose multiple gauges for the same body part.
const partNames: Record<number, readonly string[]> = {
  0: ["Head", "Torso", "Left Leg", "Right Leg", "Tail", "Left Wing", "Right Wing", "Tail"],
  1: ["Head", "Neck", "Torso", "Left Wing", "Right Wing", "Left Leg", "Right Wing", "Tail", "Unknown", "Tail", "Sky Fall", "Sky Fall", "Air Flinch", "Air Flinch", "Unknown"],
  2: ["Head", "Neck", "Torso", "Left Wing", "Right Wing", "Left Leg", "Right Wing", "Tail", "Tail", "Crash", "Crash", "Air Flinch", "Air Flinch", "Wings"],
  3: ["Head", "Neck", "Belly", "Back", "Tail", "Left Leg", "Right Leg", "Left Wing", "Right Wing", "Belly", "Belly", "Tail"],
  4: ["Head", "Neck", "Torso", "Tail", "Right Leg", "Left Leg", "Right Wing", "Left Wing"],
  5: ["Head", "Neck", "Torso", "Left Leg", "Right Leg", "Tail", "Left Wing", "Right Wing", "Head", "Head", "Feign Death", "Unknown"],
  6: ["Head", "Torso", "Left Foreleg", "Right Foreleg", "Left Hind Leg", "Right Hind Leg", "Tail"],
  7: ["Head", "Torso", "Left Foreleg", "Right Foreleg", "Left Hind Leg", "Right Hind Leg", "Tail", "Head", "Head"],
  8: ["Head", "Neck", "Chest", "Back", "Torso", "Forelegs", "Hind Legs", "Tail"],
  9: ["Head", "Torso", "Belly", "Left Claw", "Right Claw", "Left Foreleg", "Right Foreleg", "Mantle", "Unknown", "Left Hind Leg", "Right Hind Leg", "Stinger", "Unknown", "Stinger", "Stinger", "Mantle", "Ceiling Fall"]
};

export function wildsPartName(monsterId: number | undefined, partIndex: number): string | undefined {
  if (!Number.isInteger(monsterId) || !Number.isInteger(partIndex) || partIndex < 0) return undefined;
  return partNames[monsterId!]?.[partIndex];
}
