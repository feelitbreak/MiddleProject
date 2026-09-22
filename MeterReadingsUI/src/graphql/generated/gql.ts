/* eslint-disable */
import * as types from './graphql';
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';

/**
 * Map of all GraphQL operations in the project.
 *
 * This map has several performance disadvantages:
 * 1. It is not tree-shakeable, so it will include all operations in the project.
 * 2. It is not minifiable, so the string of a GraphQL query will be multiple times inside the bundle.
 * 3. It does not support dead code elimination, so it will add unused operations.
 *
 * Therefore it is highly recommended to use the babel or swc plugin for production.
 * Learn more about it here: https://the-guild.dev/graphql/codegen/plugins/presets/preset-client#reducing-bundle-size
 */
type Documents = {
    "\n  query LatestReadings($where: ReadingFilterInput) {\n    latestReadings(where: $where) {\n      id\n      collectedAt\n      co2\n      pm25\n      humidity\n      motionDetected\n      energyKwh\n      sensor {\n        id\n        name\n        type\n      }\n    }\n  }\n": typeof types.LatestReadingsDocument,
    "\n  query Catalogue {\n    locations\n    sensors {\n      id\n      name\n      type\n    }\n  }\n": typeof types.CatalogueDocument,
    "\n  query ReadingAggregates(\n    $metric: ReadingMetric!\n    $interval: AggregationInterval!\n    $where: AggregateFilterInput\n  ) {\n    readingAggregates(metric: $metric, interval: $interval, where: $where) {\n      location\n      unit\n      points {\n        periodStart\n        count\n        average\n        minimum\n        maximum\n      }\n    }\n  }\n": typeof types.ReadingAggregatesDocument,
    "\n  query ReadingsPage($first: Int!, $after: String, $where: ReadingFilterInput) {\n    readings(first: $first, after: $after, where: $where) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        collectedAt\n        co2\n        pm25\n        humidity\n        motionDetected\n        energyKwh\n        sensor {\n          id\n          name\n          type\n        }\n      }\n    }\n  }\n": typeof types.ReadingsPageDocument,
    "\n  query GatewayHealth {\n    health {\n      status\n      checks {\n        name\n        status\n      }\n    }\n  }\n": typeof types.GatewayHealthDocument,
};
const documents: Documents = {
    "\n  query LatestReadings($where: ReadingFilterInput) {\n    latestReadings(where: $where) {\n      id\n      collectedAt\n      co2\n      pm25\n      humidity\n      motionDetected\n      energyKwh\n      sensor {\n        id\n        name\n        type\n      }\n    }\n  }\n": types.LatestReadingsDocument,
    "\n  query Catalogue {\n    locations\n    sensors {\n      id\n      name\n      type\n    }\n  }\n": types.CatalogueDocument,
    "\n  query ReadingAggregates(\n    $metric: ReadingMetric!\n    $interval: AggregationInterval!\n    $where: AggregateFilterInput\n  ) {\n    readingAggregates(metric: $metric, interval: $interval, where: $where) {\n      location\n      unit\n      points {\n        periodStart\n        count\n        average\n        minimum\n        maximum\n      }\n    }\n  }\n": types.ReadingAggregatesDocument,
    "\n  query ReadingsPage($first: Int!, $after: String, $where: ReadingFilterInput) {\n    readings(first: $first, after: $after, where: $where) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        collectedAt\n        co2\n        pm25\n        humidity\n        motionDetected\n        energyKwh\n        sensor {\n          id\n          name\n          type\n        }\n      }\n    }\n  }\n": types.ReadingsPageDocument,
    "\n  query GatewayHealth {\n    health {\n      status\n      checks {\n        name\n        status\n      }\n    }\n  }\n": types.GatewayHealthDocument,
};

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 *
 *
 * @example
 * ```ts
 * const query = graphql(`query GetUser($id: ID!) { user(id: $id) { name } }`);
 * ```
 *
 * The query argument is unknown!
 * Please regenerate the types.
 */
export function graphql(source: string): unknown;

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query LatestReadings($where: ReadingFilterInput) {\n    latestReadings(where: $where) {\n      id\n      collectedAt\n      co2\n      pm25\n      humidity\n      motionDetected\n      energyKwh\n      sensor {\n        id\n        name\n        type\n      }\n    }\n  }\n"): (typeof documents)["\n  query LatestReadings($where: ReadingFilterInput) {\n    latestReadings(where: $where) {\n      id\n      collectedAt\n      co2\n      pm25\n      humidity\n      motionDetected\n      energyKwh\n      sensor {\n        id\n        name\n        type\n      }\n    }\n  }\n"];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Catalogue {\n    locations\n    sensors {\n      id\n      name\n      type\n    }\n  }\n"): (typeof documents)["\n  query Catalogue {\n    locations\n    sensors {\n      id\n      name\n      type\n    }\n  }\n"];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ReadingAggregates(\n    $metric: ReadingMetric!\n    $interval: AggregationInterval!\n    $where: AggregateFilterInput\n  ) {\n    readingAggregates(metric: $metric, interval: $interval, where: $where) {\n      location\n      unit\n      points {\n        periodStart\n        count\n        average\n        minimum\n        maximum\n      }\n    }\n  }\n"): (typeof documents)["\n  query ReadingAggregates(\n    $metric: ReadingMetric!\n    $interval: AggregationInterval!\n    $where: AggregateFilterInput\n  ) {\n    readingAggregates(metric: $metric, interval: $interval, where: $where) {\n      location\n      unit\n      points {\n        periodStart\n        count\n        average\n        minimum\n        maximum\n      }\n    }\n  }\n"];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ReadingsPage($first: Int!, $after: String, $where: ReadingFilterInput) {\n    readings(first: $first, after: $after, where: $where) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        collectedAt\n        co2\n        pm25\n        humidity\n        motionDetected\n        energyKwh\n        sensor {\n          id\n          name\n          type\n        }\n      }\n    }\n  }\n"): (typeof documents)["\n  query ReadingsPage($first: Int!, $after: String, $where: ReadingFilterInput) {\n    readings(first: $first, after: $after, where: $where) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        collectedAt\n        co2\n        pm25\n        humidity\n        motionDetected\n        energyKwh\n        sensor {\n          id\n          name\n          type\n        }\n      }\n    }\n  }\n"];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query GatewayHealth {\n    health {\n      status\n      checks {\n        name\n        status\n      }\n    }\n  }\n"): (typeof documents)["\n  query GatewayHealth {\n    health {\n      status\n      checks {\n        name\n        status\n      }\n    }\n  }\n"];

export function graphql(source: string) {
  return (documents as any)[source] ?? {};
}

export type DocumentType<TDocumentNode extends DocumentNode<any, any>> = TDocumentNode extends DocumentNode<  infer TType,  any>  ? TType  : never;