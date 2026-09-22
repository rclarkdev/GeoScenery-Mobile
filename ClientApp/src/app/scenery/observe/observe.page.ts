import { Component, OnInit } from '@angular/core';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

@Component({
  selector: 'app-observe',
  templateUrl: './observe.page.html',
  styleUrls: ['./observe.page.scss'],
})
export class ObservePage implements OnInit {

  loadedScenery: Scene[] = [];

  constructor(private sceneryService: SceneryService) { }

  ngOnInit() {
    this.sceneryService.getScenery().subscribe(scenery => this.loadedScenery = scenery);

  }

}
