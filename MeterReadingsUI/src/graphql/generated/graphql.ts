/* eslint-disable */
/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';
/**
 * Narrows which readings an aggregation covers. Deliberately not ReadingFilter: the
 * metric already fixes the sensor type, so one here could only contradict it.
 */
export type AggregateFilterInput = {
  /** Inclusive lower bound. Defaults to a window suited to the interval. */
  from?: string | null | undefined;
  location?: string | null | undefined;
  /** Exclusive upper bound. Defaults to now. */
  to?: string | null | undefined;
};

/**
 * How long one period covers. Each value names a PostgreSQL truncation unit, which is what keeps
 * period boundaries aligned to the calendar rather than to the range's start.
 */
export type AggregationInterval =
  | 'DAY'
  | 'HOUR'
  | 'MONTH'
  | 'WEEK';

export type HealthStatus =
  | 'DEGRADED'
  | 'HEALTHY'
  | 'UNHEALTHY';

/**
 * Narrows which readings a query returns. Every member is optional, so a client can hold one
 * filter object and send it unchanged.
 */
export type ReadingFilterInput = {
  /** Inclusive lower bound on collection time. */
  from?: string | null | undefined;
  location?: string | null | undefined;
  sensorType?: SensorType | null | undefined;
  /** Exclusive upper bound on collection time. */
  to?: string | null | undefined;
};

/**
 * The numeric series being aggregated. Each value implies its sensor type, which is what lets one
 * aggregation query serve every reading kind.
 */
export type ReadingMetric =
  /** CO2 concentration in ppm. */
  | 'CO2'
  /** Energy consumption in kWh. */
  | 'ENERGY_KWH'
  /** Relative humidity percentage. */
  | 'HUMIDITY'
  /** Motion as 1 or 0, so that a period's average is the fraction with motion. */
  | 'MOTION_DETECTED'
  /** PM2.5 concentration in ug/m3. */
  | 'PM25';

/**
 * The kind of sensor a reading came from. Stored as the meter_readings discriminator and
 * the sensors.sensor_type column.
 */
export type SensorType =
  /** Reports CO2, PM2.5 and humidity. */
  | 'AIR_QUALITY'
  /** Reports consumption in kWh. */
  | 'ENERGY'
  /** Reports whether motion was detected. */
  | 'MOTION';

export type LatestReadingsQueryVariables = Exact<{
  where?: ReadingFilterInput | null | undefined;
}>;


export type LatestReadingsQuery = { latestReadings: Array<{ id: number, collectedAt: string, co2: number | null, pm25: number | null, humidity: number | null, motionDetected: boolean | null, energyKwh: number | null, sensor: { id: number, name: string, type: SensorType } }> };

export type CatalogueQueryVariables = Exact<{ [key: string]: never; }>;


export type CatalogueQuery = { locations: Array<string>, sensors: Array<{ id: number, name: string, type: SensorType }> };

export type ReadingAggregatesQueryVariables = Exact<{
  metric: ReadingMetric;
  interval: AggregationInterval;
  where?: AggregateFilterInput | null | undefined;
}>;


export type ReadingAggregatesQuery = { readingAggregates: Array<{ location: string, unit: string, points: Array<{ periodStart: string, count: number, average: number, minimum: number, maximum: number }> }> };

export type ReadingsPageQueryVariables = Exact<{
  first: number;
  after?: string | null | undefined;
  where?: ReadingFilterInput | null | undefined;
}>;


export type ReadingsPageQuery = { readings: { totalCount: number, pageInfo: { hasNextPage: boolean, endCursor: string | null }, nodes: Array<{ id: number, collectedAt: string, co2: number | null, pm25: number | null, humidity: number | null, motionDetected: boolean | null, energyKwh: number | null, sensor: { id: number, name: string, type: SensorType } }> | null } };

export type GatewayHealthQueryVariables = Exact<{ [key: string]: never; }>;


export type GatewayHealthQuery = { health: { status: HealthStatus, checks: Array<{ name: string, status: HealthStatus }> } };


export const LatestReadingsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"LatestReadings"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"ReadingFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"latestReadings"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"collectedAt"}},{"kind":"Field","name":{"kind":"Name","value":"co2"}},{"kind":"Field","name":{"kind":"Name","value":"pm25"}},{"kind":"Field","name":{"kind":"Name","value":"humidity"}},{"kind":"Field","name":{"kind":"Name","value":"motionDetected"}},{"kind":"Field","name":{"kind":"Name","value":"energyKwh"}},{"kind":"Field","name":{"kind":"Name","value":"sensor"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"type"}}]}}]}}]}}]} as unknown as DocumentNode<LatestReadingsQuery, LatestReadingsQueryVariables>;
export const CatalogueDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"Catalogue"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"locations"}},{"kind":"Field","name":{"kind":"Name","value":"sensors"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"type"}}]}}]}}]} as unknown as DocumentNode<CatalogueQuery, CatalogueQueryVariables>;
export const ReadingAggregatesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"ReadingAggregates"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"metric"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"ReadingMetric"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"interval"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"AggregationInterval"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"AggregateFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"readingAggregates"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"metric"},"value":{"kind":"Variable","name":{"kind":"Name","value":"metric"}}},{"kind":"Argument","name":{"kind":"Name","value":"interval"},"value":{"kind":"Variable","name":{"kind":"Name","value":"interval"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"unit"}},{"kind":"Field","name":{"kind":"Name","value":"points"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"periodStart"}},{"kind":"Field","name":{"kind":"Name","value":"count"}},{"kind":"Field","name":{"kind":"Name","value":"average"}},{"kind":"Field","name":{"kind":"Name","value":"minimum"}},{"kind":"Field","name":{"kind":"Name","value":"maximum"}}]}}]}}]}}]} as unknown as DocumentNode<ReadingAggregatesQuery, ReadingAggregatesQueryVariables>;
export const ReadingsPageDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"ReadingsPage"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"ReadingFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"readings"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"collectedAt"}},{"kind":"Field","name":{"kind":"Name","value":"co2"}},{"kind":"Field","name":{"kind":"Name","value":"pm25"}},{"kind":"Field","name":{"kind":"Name","value":"humidity"}},{"kind":"Field","name":{"kind":"Name","value":"motionDetected"}},{"kind":"Field","name":{"kind":"Name","value":"energyKwh"}},{"kind":"Field","name":{"kind":"Name","value":"sensor"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"type"}}]}}]}}]}}]}}]} as unknown as DocumentNode<ReadingsPageQuery, ReadingsPageQueryVariables>;
export const GatewayHealthDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GatewayHealth"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"health"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"status"}},{"kind":"Field","name":{"kind":"Name","value":"checks"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"status"}}]}}]}}]}}]} as unknown as DocumentNode<GatewayHealthQuery, GatewayHealthQueryVariables>;