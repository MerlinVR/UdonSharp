// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const lightCodeTheme = require('prism-react-renderer/themes/github');
const darkCodeTheme = require('prism-react-renderer/themes/dracula');

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'UdonSharp',
  tagline: 'Compiler to make Udon Assembly from C#',
  url: 'https://udonsharp.docs.vrchat.com',
  baseUrl: '/',
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  favicon: 'images/favicon.ico',
  organizationName: 'vrchat-community', // Usually your GitHub org/user name.
  projectName: 'UdonSharp', // Usually your repo name.

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          routeBasePath: '/',
          sidebarPath: require.resolve('./sidebars.js'),
          // Please change this to your repo.
          editUrl: ({versionDocsDirPath, docPath}) =>
              `https://github.com/vrchat-community/UdonSharp/edit/master/Docs/Source/${docPath}`,
          
        },
        blog: {
          path: 'news',
          routeBasePath: 'news',
          showReadingTime: false,
          // Please change this to your repo.
          // editUrl: 'https://github.com/vrchat-community/UdonSharp',
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        defaultMode: 'dark',
        respectPrefersColorScheme: true,
      },
      announcementBar: {
        id: 'open_beta',
        content:
            '<b>This Tool is in an Open Beta, the Docs are not yet complete.</b>',
        backgroundColor: '#21af90',
        textColor: '#000',
        isCloseable: true,
      },
      navbar: {
        title: 'UdonSharp',
        logo: {
          alt: 'VRChat Logo',
          src: 'images/logo.png',
        },
        items: [
          {
            type: 'doc',
            docId: 'index',
            position: 'left',
            label: 'Docs',
          },
          {to: '/news', label: 'News', position: 'left'},
          {
            href: 'https://github.com/vrchat-community/UdonSharp',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {
                label: 'Docs',
                to: '/',
              },
            ],
          },
          {
            title: 'Community',
            items: [
              {
                label: 'Discord',
                href: 'https://discord.com/invite/vrchat',
              },
              {
                label: 'Twitter',
                href: 'https://twitter.com/vrchat',
              },
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'News',
                to: '/news',
              },
              {
                label: 'GitHub',
                href: 'https://github.com/vrchat-community/UdonSharp',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} VRChat Inc. Built with Docusaurus.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
      },
      algolia: {
        appId: 'NQHMNOH2YO',
        apiKey: '292dfc501d73d6fa1352744ce4620735',
        indexName: 'VRChat_Docs',
        contextualSearch: true,
        externalUrlRegex: 'https:\/\/(?!udonsharp)' // Results that don't come from this site should redirect using their absolute URL, rather than redirecting relative to the current site
      },
    }),
};

module.exports = config;
