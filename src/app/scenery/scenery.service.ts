import { Injectable } from '@angular/core';
import { Scene } from './scene.model';

@Injectable({
  providedIn: 'root'
})
export class SceneryService {
  private _scenery: Scene[] = [
    new Scene('s1',
    'Observation Point 1',
    'This is a cool scene',
    'https://s27363.pcdn.co/wp-content/uploads/2017/06/Observation-Point-Zion-1129x752.jpg.optimal.jpg',
    9),
    new Scene('s2',
    'Observation Point 2',
    'This is a cool scene as well',
    'https://o7fe62guj6g73vlj30xpogpm-wpengine.netdna-ssl.com/wp-content/uploads/2019/04/Black-Rock-City-Aerial-Image-2015-665x375.jpg',
    10),
    new Scene('s3',
    'Observation Point 3',
    'Another cool scene',
    'https://i.ytimg.com/vi/ruxB29F5hZY/maxresdefault.jpg',
    8)

  ];

  get scenery() {
    return [...this._scenery];
  }

  constructor() { }

  getScene(id: string) {
    return {...this._scenery.find(scene => scene.id ===  id)};
    }
  }
