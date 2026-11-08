import { closeSync, openSync, readSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { imageSize } from './topics'

/** Breite und Höhe aus dem IHDR-Block einer PNG-Datei, unabhängig vom Plugin gelesen. */
function pngSize(file: string): [number, number] {
  const head = Buffer.alloc(24)
  const fd = openSync(resolve(import.meta.dirname, 'images', `${file}.png`), 'r')
  try {
    readSync(fd, head, 0, 24, 0)
  } finally {
    closeSync(fd)
  }
  return [head.readUInt32BE(16), head.readUInt32BE(20)]
}

describe('Maße der Hilfebilder (Befund B22)', () => {
  it('Given ein Handbuchbild When seine Maße abgefragt werden Then stimmen sie mit dem PNG-Kopf überein', () => {
    for (const name of ['web/myday-heute', 'web/myday-karte']) {
      const [width, height] = pngSize(name)
      expect(imageSize(name)).toEqual({ width, height })
    }
  })

  it('Given ein Bild, das es nicht gibt When seine Maße abgefragt werden Then gibt es keine', () => {
    expect(imageSize('web/gibt-es-nicht')).toBeUndefined()
  })
})
