import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { SceneDetailPage } from './scene-detail.page';

describe('SceneDetailPage', () => {
  let component: SceneDetailPage;
  let fixture: ComponentFixture<SceneDetailPage>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ SceneDetailPage ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
