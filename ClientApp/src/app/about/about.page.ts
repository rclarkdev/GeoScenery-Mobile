import { Component } from '@angular/core';

@Component({
  selector: 'app-about',
  templateUrl: './about.page.html',
  styleUrls: ['./about.page.scss']
})
export class AboutPage {
  readonly features = [
    {
      icon: 'map-outline',
      title: 'Find a scene your way',
      description: 'Browse the map or search by place, distance, and tags.'
    },
    {
      icon: 'star-outline',
      title: 'Leave a thoughtful rating',
      description: 'Rate a scene and add a note if you feel like it.'
    },
    {
      icon: 'chatbubbles-outline',
      title: 'Keep in touch',
      description: 'Follow profiles and message other explorers.'
    },
    {
      icon: 'footsteps-outline',
      title: 'Save places you like',
      description: 'Add a visit when you choose, and remove it whenever you like.'
    }
  ];
}