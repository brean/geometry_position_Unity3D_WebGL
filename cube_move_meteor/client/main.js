import { Template } from 'meteor/templating';
import { ReactiveVar } from 'meteor/reactive-var';
import { Visualization } from '/imports/Visualization.js';
import { Geometry } from '/imports/api/geometry/geometry.js';

import './main.html';

Template.visualization.onCreated(function() {
  Meteor.subscribe('geometry');
  this.vis = new ReactiveVar();
});

function onPositionChange(event) {
  Meteor.call('geometry.move', event.docId, event.position);
}

Template.visualization.onRendered(function() {
  let vis = new Visualization();
  $(window).resize(
    function() {
    setTimeout(function() {
      $('canvas').height($( window ).height());
      $('canvas').width($( window ).width());
      vis.onWindowResize();
    }, 250);
  });
  vis.addEventListener('positionChange', onPositionChange.bind(vis));
  let geometry = Geometry.find({});
  geometry.observeChanges({
    removed: function(docId) {
      vis.removeGeometry(docId);
    },
    changed: function(docId, newDoc) {
      vis.changeGeometry(docId, newDoc);
    },
    added: function(docId, newDoc) {
      vis.addGeometry(docId, newDoc);
    }
  });
  Template.instance().vis.set(vis);
});

Template.visualization.helpers({

});

Template.visualization.events({
  'click button'(event, instance) {
    Meteor.call('geometry.add');
  },
});
