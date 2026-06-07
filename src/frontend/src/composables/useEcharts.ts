import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { BarChart, GaugeChart } from 'echarts/charts';
import {
  TitleComponent,
  TooltipComponent,
  GridComponent,
} from 'echarts/components';

// Module-level guard: echarts/core.use() warns on duplicate registration
// and the registered set is global, so doing it per component is wasteful
// AND triggers the duplicate-registration warning when a view is mounted
// more than once. A boolean flag keeps the call idempotent and side-effect
// free for subsequent mounts.
let registered = false;

/**
 * Registers the ECharts modules used across the app. Idempotent: subsequent
 * calls are no-ops. Called once from main.ts at boot.
 */
export function useEcharts(): void {
  if (registered) return;
  use([
    CanvasRenderer,
    BarChart,
    GaugeChart,
    TitleComponent,
    TooltipComponent,
    GridComponent,
  ]);
  registered = true;
}
