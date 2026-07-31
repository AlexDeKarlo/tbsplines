import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, dirname, posix } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const SOURCE_ROOTS = ['Runtime', 'Editor', 'Samples~/Examples']

const problems = []
const guids = new Map()

function check(dir) {
  for (const name of readdirSync(dir).sort()) {
    const path = join(dir, name)
    const relative = posix.normalize(path.slice(root.length + 1).split('\\').join('/'))
    if (name.endsWith('.meta')) {
      const owner = path.slice(0, -5)
      if (!existsSync(owner)) problems.push(`orphan meta, its asset is gone: ${relative}`)
      const text = readFileSync(path, 'utf8')
      const match = text.match(/^guid:\s*([0-9a-fA-F]{32})\s*$/m)
      if (!match) problems.push(`meta has no usable guid: ${relative}`)
      else if (guids.has(match[1])) problems.push(`duplicate guid ${match[1]}: ${relative} and ${guids.get(match[1])}`)
      else guids.set(match[1], relative)
      continue
    }
    if (!existsSync(`${path}.meta`)) problems.push(`asset has no meta, its guid would change on every install: ${relative}`)
    if (statSync(path).isDirectory()) check(path)
  }
}

for (const source of SOURCE_ROOTS) {
  const dir = join(root, source)
  if (!existsSync(dir)) problems.push(`missing source root: ${source}`)
  else check(dir)
}

const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'))
if (!/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(manifest.version))
  problems.push(`package.json version is not semver: ${manifest.version}`)
for (const field of ['name', 'displayName', 'description', 'unity']) {
  if (!manifest[field]) problems.push(`package.json is missing ${field}`)
}
for (const sample of manifest.samples ?? []) {
  if (!existsSync(join(root, sample.path))) problems.push(`sample path does not exist: ${sample.path}`)
}

const tag = process.argv[2]
if (tag && tag.replace(/^v/, '') !== manifest.version)
  problems.push(`tag ${tag} does not match package.json version ${manifest.version}`)

if (problems.length > 0) {
  console.error(`Package validation failed:\n${problems.map(p => `  - ${p}`).join('\n')}`)
  process.exit(1)
}

console.log(`Package looks good: ${guids.size} assets with stable guids, version ${manifest.version}`)
