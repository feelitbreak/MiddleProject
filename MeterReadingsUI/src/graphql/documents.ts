import { graphql } from './generated';

/**
 * One filter object drives every panel, so each document takes the same `where` shape the gateway
 * defines rather than a per-panel variant.
 */
export const LATEST_READINGS = graphql(`
  query LatestReadings($where: ReadingFilterInput) {
    latestReadings(where: $where) {
      id
      collectedAt
      co2
      pm25
      humidity
      motionDetected
      energyKwh
      sensor {
        id
        name
        type
      }
    }
  }
`);

export const CATALOGUE = graphql(`
  query Catalogue {
    locations
    sensors {
      id
      name
      type
    }
  }
`);

export const READING_AGGREGATES = graphql(`
  query ReadingAggregates(
    $metric: ReadingMetric!
    $interval: AggregationInterval!
    $where: AggregateFilterInput
  ) {
    readingAggregates(metric: $metric, interval: $interval, where: $where) {
      location
      unit
      points {
        periodStart
        count
        average
        minimum
        maximum
      }
    }
  }
`);

export const READINGS_PAGE = graphql(`
  query ReadingsPage($first: Int!, $after: String, $where: ReadingFilterInput) {
    readings(first: $first, after: $after, where: $where) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        id
        collectedAt
        co2
        pm25
        humidity
        motionDetected
        energyKwh
        sensor {
          id
          name
          type
        }
      }
    }
  }
`);

export const GATEWAY_HEALTH = graphql(`
  query GatewayHealth {
    health {
      status
      checks {
        name
        status
      }
    }
  }
`);
