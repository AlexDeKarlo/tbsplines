import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const version = (process.argv[2] ?? '').replace(/^v/, '')
if (!version) {
  console.error('usage: node changelog-section.mjs <version>')
  process.exit(1)
}

const changelog = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'CHANGELOG.md'), 'utf8')
const lines = changelog.split(/\r?\n/)

const start = lines.findIndex(line => new RegExp(`^##\\s*\\[?${version.replace(/\./g, '\\.')}\\]?`).test(line))
if (start < 0) {
  console.error(`CHANGELOG.md has no section for ${version}. Add one before releasing.`)
  process.exit(1)
}

let end = lines.length
for (let i = start + 1; i < lines.length; i++) {
  if (/^##\s/.test(lines[i])) { end = i; break }
}

const body = lines.slice(start + 1, end).join('\n').trim()
if (!body) {
  console.error(`The CHANGELOG.md section for ${version} is empty.`)
  process.exit(1)
}

console.log(body)
