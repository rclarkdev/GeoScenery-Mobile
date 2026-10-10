import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed, waitForAsync } from '@angular/core/testing';

import { Platform } from '@ionic/angular';

import { AppComponent } from './app.component';
import { SiteIdentityService } from './platform/site-identity.service';

describe('AppComponent', () => {

  let platformReadySpy, platformSpy;
  let siteIdentitySpy: jasmine.SpyObj<SiteIdentityService>;

  beforeEach(waitForAsync(() => {
    platformReadySpy = Promise.resolve();
    platformSpy = jasmine.createSpyObj('Platform', { ready: platformReadySpy });
    siteIdentitySpy = jasmine.createSpyObj<SiteIdentityService>('SiteIdentityService', ['initialize']);
    siteIdentitySpy.initialize.and.resolveTo(undefined);

    TestBed.configureTestingModule({
      declarations: [AppComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [
        { provide: Platform, useValue: platformSpy },
        { provide: SiteIdentityService, useValue: siteIdentitySpy },
      ],
    }).compileComponents();
  }));

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.debugElement.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should initialize the app', async () => {
    TestBed.createComponent(AppComponent);
    expect(platformSpy.ready).toHaveBeenCalled();
    await platformReadySpy;
  });

  it('starts optional platform identity loading without awaiting it', () => {
    siteIdentitySpy.initialize.and.returnValue(new Promise<void>(() => undefined));

    const fixture = TestBed.createComponent(AppComponent);

    expect(siteIdentitySpy.initialize).toHaveBeenCalled();
    expect(fixture.componentInstance).toBeTruthy();
  });

  // TODO: add more tests!

});
