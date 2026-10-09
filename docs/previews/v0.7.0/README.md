# ViVeUI 0.7.0 native Windows evidence

These 218 PNGs and reports were produced by [Windows CI](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37892796234) from `bc329e4da9e7f447206008460f760d39bdcf8297`. [PROVENANCE.json](PROVENANCE.json) records repository-byte hashes. A later documentation-only commit includes this evidence without changing the implementation.

- [Chinese feature homepage](recipes/zh-Hans/full.png), [compact](recipes/zh-Hans/compact.png)
- [English homepage](recipes/en/full.png), [Arabic compact RTL](recipes/ar/compact.png)
- [Two-ID enable](recipes/group-enabled.png), [partial failure](recipes/group-partial.png)
- [Recipe assertions](recipe-result.json), [real cross-version update](self-update-result.json)

Feature controls use isolated fake storage; the screenshots do not prove experimental Windows feature behavior. Self-update tests use real official executable copies in an isolated temporary directory. They do not exercise the live-metadata helper or secure-desktop UAC consent. [Full validation scope](../../VALIDATION.md).
