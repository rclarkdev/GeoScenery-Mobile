import { Component } from '@angular/core';

@Component({
  selector: 'app-about',
  templateUrl: './about.page.html'
})
export class AboutPage {
  readonly features = [
    {
      icon: 'camera-outline',
      title: 'Capture the moment',
      description: 'Create scenes with photos from your device and add the place where they happened.'
    },
    {
      icon: 'map-outline',
      title: 'Explore nearby',
      description: 'Search the community by location, distance, and tags with an interactive map.'
    },
    {
      icon: 'people-outline',
      title: 'Share with people',
      description: 'Follow other explorers, discover their scenes, and exchange thoughtful ratings.'
    }
  ];
}