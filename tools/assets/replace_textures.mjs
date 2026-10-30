import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import fs from 'fs';
const [,, inp, out, png, ...names] = process.argv;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(inp);
const img = fs.readFileSync(png);
for (const t of doc.getRoot().listTextures()) if (names.includes(t.getName())) { t.setImage(img).setMimeType('image/png').setURI(''); console.log('replaced', t.getName()); }
await io.write(out, doc);
