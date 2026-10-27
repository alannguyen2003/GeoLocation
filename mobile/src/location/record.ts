import { enqueue } from './queue';
import { recordLastPoint } from './status';
import type { QueuedPoint } from './types';
import { flushQueue, type FlushOptions } from './uploader';

/** Every new point goes into the queue first, then the uploader tries to send it. */
export async function recordPoints(points: QueuedPoint[], flushOptions?: FlushOptions): Promise<void> {
  if (points.length === 0) {
    return;
  }

  await enqueue(points);
  await recordLastPoint(points[points.length - 1]);
  await flushQueue(flushOptions);
}
