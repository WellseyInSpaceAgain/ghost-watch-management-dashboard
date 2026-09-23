import {Component,ElementRef,afterRenderEffect,input,viewChild} from '@angular/core';
import {Chart,BarController,BarElement,LineController,LineElement,PointElement,CategoryScale,LinearScale,PieController,DoughnutController,ArcElement,Tooltip,Legend} from 'chart.js';
Chart.register(BarController,BarElement,LineController,LineElement,PointElement,CategoryScale,LinearScale,PieController,DoughnutController,ArcElement,Tooltip,Legend);
export interface ChartData {title:string;description:string|null;type:'line'|'bar'|'stackedBar'|'pie'|'donut'|'kpi';xLabel:string|null;labels:string[];series:{label:string;format:string;values:(number|null)[]}[];rowCount:number;notice:string|null;}
export function chartValue(value:number|null|undefined,format:string):string {if(value==null)return 'Unknown';const text=new Intl.NumberFormat('en-GB',{maximumFractionDigits:2}).format(value);return text+(({isk:' ISK',percent:'%',days:' days'} as Record<string,string>)[format]??'');}
@Component({selector:'app-chart-renderer',template:`
<h3>{{data().title}}</h3>@if(data().description){<p class="muted">{{data().description}}</p>}
@if(data().notice){<p class="notice">{{data().notice}}</p>}
@if(data().type==='kpi'){<strong class="kpi">{{value(data().series[0]?.values?.[0],data().series[0]?.format??'number')}}</strong>}
@else if(hasValues()){<div class="canvas-wrap"><canvas #canvas role="img" [attr.aria-label]="data().title+'; data available in table below'"></canvas></div>}
@else if(data().rowCount>0){<p>Chart unavailable until its values are recorded.</p>}
<details><summary>Chart data ({{data().rowCount}} source records)</summary><div class="table-wrap"><table><thead><tr><th>{{data().xLabel??'Group'}}</th>@for(series of data().series;track $index){<th>{{series.label}}</th>}</tr></thead><tbody>@for(label of data().labels;track $index;let index=$index){<tr><td>{{label}}</td>@for(series of data().series;track $index){<td>{{value(series.values[index],series.format)}}</td>}</tr>}@empty{<tr><td [attr.colspan]="data().series.length+1">No matching records.</td></tr>}</tbody></table></div></details>
`,styles:`:host{display:block;min-width:0;}h3{margin-top:0;}.canvas-wrap{position:relative;height:280px;min-width:0;}details{margin-top:14px;}summary{cursor:pointer;}.kpi{display:block;font-size:2rem;margin:20px 0;overflow-wrap:anywhere;}`})
export class ChartRenderer {
 readonly data=input.required<ChartData>();private readonly canvas=viewChild<ElementRef<HTMLCanvasElement>>('canvas');readonly value=chartValue;
 hasValues(){return this.data().series.some(s=>s.values.some(x=>x!==null));}
 constructor(){afterRenderEffect(onCleanup=>{
  const data=this.data();const canvas=this.canvas()?.nativeElement;if(!canvas||data.type==='kpi')return;
  const circular=data.type==='pie'||data.type==='donut';const colours=['#59c5bc','#e7b96b','#8fa9ef','#da91b3','#a3c96a','#a3b7c5'];
  const chart=new Chart<'bar'|'line'|'pie'|'doughnut',(number|null)[]>(canvas,{type:data.type==='stackedBar'?'bar':data.type==='donut'?'doughnut':data.type,
   data:{labels:data.labels,datasets:data.series.map((series,index)=>({label:series.label,data:series.values,borderColor:circular?colours:colours[index],backgroundColor:circular?colours:colours[index],borderWidth:2,spanGaps:false}))},
   options:{responsive:true,maintainAspectRatio:false,animation:false,color:'#d6e3e8',plugins:{legend:{labels:{color:'#d6e3e8'}},tooltip:{callbacks:{label:context=>`${context.dataset.label}: ${chartValue(context.raw as number|null,data.series[context.datasetIndex].format)}`}}},scales:circular?{}:{x:{stacked:data.type==='stackedBar',ticks:{color:'#a9bcc5'},grid:{color:'#29353d'},title:{display:!!data.xLabel,text:data.xLabel??'',color:'#a9bcc5'}},y:{stacked:data.type==='stackedBar',ticks:{color:'#a9bcc5'},grid:{color:'#29353d'}}}}});
  onCleanup(()=>chart.destroy());
 });}
}
