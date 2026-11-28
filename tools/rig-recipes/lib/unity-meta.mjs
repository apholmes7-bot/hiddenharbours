// A meta copied from another texture as a template must not carry that texture's own sprite records. The landing
// deck's metas hold a sprite named after the deck with the deck's rect (992 x 358), which a smaller picture copied
// whole would claim too. Single-mode sprites need no record of their own: the importer makes the one sprite from
// the whole texture. So the sheet's records and its name table are emptied, and nothing else is touched (the
// GUID is the caller's, the import settings are the template's).
export function withoutTemplateSprites(meta) {
  return meta
    .replace(/^( {4}sprites:)\n(?: {4}- [\s\S]*?)(?=^ {4}outline:)/m, '$1 []\n')
    .replace(/^( {4}nameFileIdTable:)\n(?: {6}.*\n)+/m, '$1 {}\n');
}
