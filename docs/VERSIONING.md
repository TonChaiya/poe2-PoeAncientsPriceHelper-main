# Versioning policy

This fork uses Semantic Versioning independently from upstream and starts at `1.0.0`.

- **MAJOR**: incompatible configuration/workflow change or major architecture break.
- **MINOR**: backward-compatible feature or substantial capability.
- **PATCH**: backward-compatible fix, performance improvement, or documentation correction.

Every release increments from the latest fork version. Update the `.csproj`, installer scripts, current changelog, and UI labels together. Add an immutable `docs/releases/X.Y.Z.md` record containing provenance, features, fixes, verification, installer name, and checksum. Never renumber using an upstream release and never rewrite a published version record; later corrections belong in a new release.

