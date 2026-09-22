import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';
import prettier from 'eslint-config-prettier';
import sonarjs from 'eslint-plugin-sonarjs';

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'src/graphql/generated']),
  {
    files: ['**/*.{ts,tsx}'],
    // SonarJS runs the same rules SonarQube enforces in CI, mirroring how every .NET project here
    // references SonarAnalyzer.CSharp so the server's findings surface locally first.
    extends: [
      js.configs.recommended,
      tseslint.configs.recommendedTypeChecked,
      sonarjs.configs.recommended,
      prettier,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        project: ['./tsconfig.app.json', './tsconfig.test.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-deprecated': 'warn',
    },
  },
  {
    files: ['*.config.{js,ts}', 'codegen.ts', 'jest.config.ts'],
    extends: [tseslint.configs.disableTypeChecked],
  },
  {
    files: ['**/*.test.{ts,tsx}', 'jest.setup.ts'],
    extends: [tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      globals: { ...globals.jest, ...globals.node },
      parserOptions: {
        project: ['./tsconfig.test.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
]);
