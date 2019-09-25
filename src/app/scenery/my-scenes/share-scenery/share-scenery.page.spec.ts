import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ShareSceneryPage } from './share-scenery.page';

describe('ShareSceneryPage', () => {
  let component: ShareSceneryPage;
  let fixture: ComponentFixture<ShareSceneryPage>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ShareSceneryPage ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ShareSceneryPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
