import {Pipe,PipeTransform} from '@angular/core';
export function ratioValue(value:number):string {return new Intl.NumberFormat('en-GB',{maximumSignificantDigits:8}).format(value);}
@Pipe({name:'metric'})
export class MetricPipe implements PipeTransform {transform(value:number|null|undefined,unit='ISK'):string{return value==null?'Unknown':`${unit==='ISK/slot-day/ISK'?ratioValue(value):new Intl.NumberFormat('en-GB',{maximumFractionDigits:2}).format(value)}${unit==='count'?'':` ${unit}`}`;}}
export interface MetricDefinition {key:string;label:string;unit:string;}
export interface TrackSummary {id:string;name:string;status:string;purpose:string;poolId:string|null;poolName:string|null;metrics:Record<string,number|null>;selectedKpis:string[];kpiRevision:number;}
export interface ProgrammeSummary {timestamp:string;metrics:Record<string,number|null>;tracks:TrackSummary[];replacementPackageName:string|null;facts:{liquid:number|null;knownLiquid:number;walletCount:number;characterCount:number;walletsStale:boolean;marketBuyCommitments:number|null;sellOrderListedValue:number|null;ordersStale:boolean};}
