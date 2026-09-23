import { Component, computed, input } from '@angular/core';
import { marked } from 'marked';
@Component({selector:'app-markdown-view',template:`<div class="markdown" [innerHTML]="html()"></div>`,styles:`.markdown{overflow-wrap:anywhere;line-height:1.6;} :host{display:block;min-width:0;}`})
export class MarkdownView {
  readonly body=input('');
  // Angular sanitises this string binding. Never bypass HTML security for Markdown.
  readonly html=computed(()=>marked.parse(this.body(),{async:false,gfm:true}));
}
