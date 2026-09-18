import type { CodegenConfig } from '@graphql-codegen/cli';

/**
 * Types come from the gateway's committed schema rather than a running server, so `npm run codegen`
 * works offline and a schema change shows up as a compile error here.
 */
const config: CodegenConfig = {
  schema: '../GraphQLGatewayService/src/Api/schema.graphql',
  documents: ['src/**/*.ts', 'src/**/*.tsx', '!src/graphql/generated/**'],
  ignoreNoDocuments: true,
  generates: {
    './src/graphql/generated/': {
      preset: 'client',
      config: {
        useTypeImports: true,
        skipTypename: false,
        // Without these the two custom scalars generate as `unknown`. DateTime arrives as an
        // ISO-8601 string; Long is a 64-bit id that HotChocolate serialises as a JSON number.
        scalars: { DateTime: 'string', Long: 'number' },
      },
    },
  },
};

export default config;
