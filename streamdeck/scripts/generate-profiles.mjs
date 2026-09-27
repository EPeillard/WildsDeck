import { createHash } from "node:crypto";
import { readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const pluginDirectory = "com.wildsdeck.streamdeck.sdPlugin";
const profileName = "WildsDeck";
const layoutNames = ["town", "hunt"];
const legacyArchives = ["WildsDeck - Town.streamDeckProfile", "WildsDeck - Hunt.streamDeckProfile"];
const crcTable = Array.from({ length: 256 }, (_, index) => {
  let value = index;
  for (let bit = 0; bit < 8; bit++) value = (value & 1) ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
  return value >>> 0;
});

const layouts = [];
for (const layoutName of layoutNames) {
  const layout = JSON.parse(await readFile(`profiles/${layoutName}.layout.json`, "utf8"));
  validateLayout(layout);
  layouts.push({ layoutName, layout });
}

const rootId = stableUuid(`${profileName}:root`).toUpperCase();
const root = `${rootId}.sdProfile`;
const pages = layouts.map(({ layoutName, layout }) => {
  const pageId = stableUuid(`${profileName}:${layoutName}:page`);
  const pageFolderId = profileFolderId(pageId);
  const actions = Object.fromEntries(layout.keys.map((key) => [
    `${key.x},${key.y}`,
    profileAction(`${profileName}:${layoutName}`, key)
  ]));
  return {
    pageId,
    pageFolderId,
    manifest: { Controllers: [{ Actions: actions, Type: "Keypad" }], Icon: "", Name: "" }
  };
});

// Keep the bundled archive device-agnostic. The plugin manifest's DeviceType=0
// already limits this profile to standard 5x3 Stream Deck devices.
const profileManifest = {
  Name: profileName,
  Pages: { Current: pages[0].pageId, Pages: pages.map((page) => page.pageId) },
  Version: "2.0"
};

const files = [
  [`${root}/manifest.json`, JSON.stringify(profileManifest)],
  ...pages.map((page) => [
    `${root}/Profiles/${page.pageFolderId}/manifest.json`,
    JSON.stringify(page.manifest)
  ])
];

await Promise.all(legacyArchives.map((archive) => rm(path.join(pluginDirectory, archive), { force: true })));
const destination = path.join(pluginDirectory, `${profileName}.streamDeckProfile`);
await writeFile(destination, zip(files));
console.log(`Generated ${destination} with ${pages.length} pages (${layoutNames.join(", ")})`);

function profileAction(pageName, key) {
  return {
    ActionID: stableUuid(`${pageName}:${key.x},${key.y}`),
    LinkedTitle: true,
    Name: "Wilds Display",
    Settings: { metric: key.metric, label: key.label },
    State: 0,
    States: [{
      FontFamily: "",
      FontSize: 9,
      FontStyle: "",
      FontUnderline: false,
      OutlineThickness: 2,
      ShowTitle: false,
      TitleAlignment: "middle",
      TitleColor: "#ffffff"
    }],
    UUID: "com.wildsdeck.streamdeck.metric"
  };
}

function validateLayout(layout) {
  if (layout.columns !== 5 || layout.rows !== 3 || layout.keys.length !== 15) throw new Error(`${layout.name} must define exactly 15 keys.`);
  const positions = new Set();
  for (const key of layout.keys) {
    if (key.x < 0 || key.x >= 5 || key.y < 0 || key.y >= 3) throw new Error(`Invalid key position ${key.x},${key.y}.`);
    const position = `${key.x},${key.y}`;
    if (positions.has(position)) throw new Error(`Duplicate key position ${position}.`);
    positions.add(position);
  }
}

function stableUuid(input) {
  const bytes = createHash("sha1").update(`wildsdeck:${input}`).digest().subarray(0, 16);
  bytes[6] = (bytes[6] & 0x0f) | 0x50;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = bytes.toString("hex");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function profileFolderId(uuid) {
  return ((uuid.replaceAll("-", "") + "000").match(/.{5}/g) ?? [])
    .map((group) => Number.parseInt(group, 16).toString(32).padStart(4, "0"))
    .join("")
    .slice(0, 26)
    .toUpperCase()
    .replaceAll("V", "W")
    .replaceAll("U", "V") + "Z";
}

function zip(files) {
  const localParts = [];
  const centralParts = [];
  let offset = 0;
  for (const [filename, contents] of files) {
    const name = Buffer.from(filename, "utf8");
    const data = Buffer.from(contents, "utf8");
    const checksum = crc32(data);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(0, 8);
    local.writeUInt16LE(0, 10);
    local.writeUInt16LE(0x0021, 12);
    local.writeUInt32LE(checksum, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(name.length, 26);
    localParts.push(local, name, data);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(0, 10);
    central.writeUInt16LE(0, 12);
    central.writeUInt16LE(0x0021, 14);
    central.writeUInt32LE(checksum, 16);
    central.writeUInt32LE(data.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt32LE(offset, 42);
    centralParts.push(central, name);
    offset += local.length + name.length + data.length;
  }
  const central = Buffer.concat(centralParts);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(central.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...localParts, central, end]);
}

function crc32(data) {
  let value = 0xffffffff;
  for (const byte of data) value = crcTable[(value ^ byte) & 0xff] ^ (value >>> 8);
  return (value ^ 0xffffffff) >>> 0;
}
