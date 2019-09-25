import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { NewScenePage } from './new-scene.page';

describe('NewScenePage', () => {
  let component: NewScenePage;
  let fixture: ComponentFixture<NewScenePage>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ NewScenePage ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(NewScenePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
