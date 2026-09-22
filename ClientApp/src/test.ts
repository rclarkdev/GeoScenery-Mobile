// This file is required by karma.conf.js and loads recursively all the .spec and framework files

import 'zone.js/testing';
import { getTestBed } from '@angular/core/testing';
import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting
} from '@angular/platform-browser-dynamic/testing';

declare const require: any;

// First, initialize the Angular testing environment.
getTestBed().initTestEnvironment(
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting(), {
    teardown: { destroyAfterEach: false }
}
);
// Then we find all the tests.
// And load the modules.
import './app/app.component.spec';
import './app/auth/auth.service.spec';
import './app/auth/auth.page.spec';
import './app/scenery/scenery.page.spec';
import './app/scenery/scenery.service.spec';
import './app/scenery/observe/observe.page.spec';
import './app/scenery/observe/scene-detail/scene-detail.page.spec';
import './app/scenery/my-scenes/my-scenes.page.spec';
import './app/scenery/my-scenes/new-scene/new-scene.page.spec';
import './app/scenery/my-scenes/edit-scene/edit-scene.page.spec';
import './app/scenery/my-scenes/share-scenery/share-scenery.page.spec';
import './app/visits/visits.page.spec';
import './app/visits/visits.service.spec';
