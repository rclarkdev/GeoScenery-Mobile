import { Component, OnInit } from '@angular/core';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

@Component({
  selector: 'app-observe',
  templateUrl: './observe.page.html',
  styleUrls: ['./observe.page.scss'],
})
export class ObservePage implements OnInit {

  loadedScenery: Scene[];

  constructor(private sceneryService: SceneryService) { }

  ngOnInit() {
    // TODO: update this to handle page caching

    this.loadedScenery = this.sceneryService.scenery;

  }

}
