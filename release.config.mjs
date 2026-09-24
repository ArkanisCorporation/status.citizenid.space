/**
 * @type {import('semantic-release').GlobalConfig}
 */
export default {
    branches: [
        "release/+([0-9])?(.{+([0-9]),x}).x",
        {
            name: "release/stable",
            channel: "stable"
        },
        {
            name: "main",
            channel: "staging",
            prerelease: "dev"
        },
        {
            name: "ci",
            channel: "ci",
            prerelease: "ci-do-not-use"
        },
    ],
    repositoryUrl: "https://github.com/ArkanisCorporation/status.citizenid.space",
    tagFormat: "v${version}",
    debug: false,
    plugins: [
        "@semantic-release/commit-analyzer",
        "@semantic-release/release-notes-generator",
        "@semantic-release/github",
    ],
};
