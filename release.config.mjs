/**
 * @type {import('semantic-release').GlobalConfig}
 */
export default {
    branches: [
        {
            name: "+([0-9])?(.{+([0-9]),x}).x",
            channel: "stable"
        },
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
            name: ".+",
            channel: "ci",
            prerelease: "ci-do-not-use"
        },
    ],
    repositoryUrl: "https://github.com/ArkanisCorporation/Template.NET",
    tagFormat: "v${version}",
    debug: false,
    plugins: [
        "@semantic-release/commit-analyzer",
        "@semantic-release/release-notes-generator",
        "@semantic-release/github",
    ],
};
