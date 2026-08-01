import { createHash } from 'node:crypto'
import { gzipSync } from 'node:zlib'
import { readFileSync, writeFileSync, readdirSync, statSync, mkdirSync } from 'node:fs'
import { join, posix, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')

const ROOTS = [
  { from: 'Runtime', to: 'Assets/TBSplineS/Runtime' },
  { from: 'Editor', to: 'Assets/TBSplineS/Editor' },
  { from: 'Samples~/Examples', to: 'Assets/TBSplineS/Examples' },
]

const STORE = process.argv.includes('--store')

const LOOSE = [
  { from: 'README.md', to: 'Assets/TBSplineS/README.md' },
  { from: 'CHANGELOG.md', to: 'Assets/TBSplineS/CHANGELOG.md' },
]

// On the Asset Store the terms come from Unity's End User License Agreement, so the
// repository's own licence is replaced rather than shipped alongside it.
const STORE_LICENSE = `TBSplineS

Distributed through the Unity Asset Store under the Asset Store
End User License Agreement: https://unity.com/legal/as-terms

Copyright (c) 2026 AlexDeKarlo. All rights reserved.

Source, issues and updates: https://github.com/AlexDeKarlo/tbsplines
`

function guidFor(assetPath) {
  return createHash('md5').update(`com.thebestsplinesolution.core:${assetPath}`).digest('hex')
}

function readMeta(metaPath) {
  try {
    const text = readFileSync(metaPath, 'utf8')
    const match = text.match(/^guid:\s*([0-9a-fA-F]{32})\s*$/m)
    return match ? { text, guid: match[1] } : null
  } catch {
    return null
  }
}

function synthesizeMeta(assetPath, isFolder) {
  const guid = guidFor(assetPath)
  const body = isFolder ? 'folderAsset: yes\n' : ''
  const importer = isFolder
    ? 'DefaultImporter:\n  externalObjects: {}\n'
    : 'TextScriptImporter:\n  externalObjects: {}\n'
  return { guid, text: `fileFormatVersion: 2\nguid: ${guid}\n${body}${importer}  userData: \n  assetBundleName: \n  assetBundleVariant: \n` }
}

const entries = []

function addEntry(diskPath, assetPath, isFolder, content) {
  const meta = readMeta(`${diskPath}.meta`) ?? synthesizeMeta(assetPath, isFolder)
  entries.push({
    guid: meta.guid,
    meta: meta.text,
    pathname: assetPath,
    data: isFolder ? null : content ?? readFileSync(diskPath),
  })
}

function walk(diskDir, assetDir) {
  addEntry(diskDir, assetDir, true)
  for (const name of readdirSync(diskDir).sort()) {
    if (name.endsWith('.meta')) continue
    const diskPath = join(diskDir, name)
    const assetPath = posix.join(assetDir, name)
    if (statSync(diskPath).isDirectory()) walk(diskPath, assetPath)
    else addEntry(diskPath, assetPath, false)
  }
}

for (const { from, to } of ROOTS) walk(join(root, from), to)
for (const { from, to } of LOOSE) addEntry(join(root, from), to, false)

if (STORE) addEntry(join(root, 'LICENSE.md'), 'Assets/TBSplineS/LICENSE.txt', false, Buffer.from(STORE_LICENSE, 'utf8'))
else addEntry(join(root, 'LICENSE.md'), 'Assets/TBSplineS/LICENSE.md', false)

function tarHeader(name, size) {
  const header = Buffer.alloc(512)
  header.write(name, 0, 100, 'utf8')
  header.write('0000644\0', 100, 8, 'utf8')
  header.write('0000000\0', 108, 8, 'utf8')
  header.write('0000000\0', 116, 8, 'utf8')
  header.write(size.toString(8).padStart(11, '0') + '\0', 124, 12, 'utf8')
  header.write('00000000000\0', 136, 12, 'utf8')
  header.write('        ', 148, 8, 'utf8')
  header.write(size === null ? '5' : '0', 156, 1, 'utf8')
  header.write('ustar\0' + '00', 257, 8, 'utf8')
  let sum = 0
  for (const byte of header) sum += byte
  header.write(sum.toString(8).padStart(6, '0') + '\0 ', 148, 8, 'utf8')
  return header
}

const chunks = []

function tarFile(name, contents) {
  const body = Buffer.isBuffer(contents) ? contents : Buffer.from(contents, 'utf8')
  chunks.push(tarHeader(name, body.length), body)
  const padding = (512 - (body.length % 512)) % 512
  if (padding > 0) chunks.push(Buffer.alloc(padding))
}

const seen = new Set()
for (const entry of entries.sort((a, b) => a.pathname.localeCompare(b.pathname))) {
  if (seen.has(entry.guid)) throw new Error(`Duplicate GUID ${entry.guid} at ${entry.pathname}`)
  seen.add(entry.guid)
  if (entry.data !== null) tarFile(`${entry.guid}/asset`, entry.data)
  tarFile(`${entry.guid}/asset.meta`, entry.meta)
  tarFile(`${entry.guid}/pathname`, entry.pathname)
}
chunks.push(Buffer.alloc(1024))

const version = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).version
const outDir = join(root, 'Build')
mkdirSync(outDir, { recursive: true })
const outPath = join(outDir, `TBSplineS-${version}${STORE ? '-store' : ''}.unitypackage`)
writeFileSync(outPath, gzipSync(Buffer.concat(chunks), { level: 9 }))

console.log(`${outPath}  (${entries.length} assets, ${(statSync(outPath).size / 1024).toFixed(0)} KB)`)
