import { Meteor } from 'meteor/meteor';
import { Geometry } from './geometry';

Meteor.methods({
  'geometry.add': function(type='cube', position=[0, 0, 0], rotation=[0, 0, 0]) {
    // todo: merge with responded/response_to?
    Geometry.insert({
      created: new Date(),
      type: type,
      position: position,
      rotation: rotation
    });
  },
  'geometry.deleteAll': function() {
    Geometry.remove({});
  },
  'geometry.rotate': function(_id, newRotation) {
    let geometry = Geometry.findOne({_id: _id});
    if (!geometry) {
      console.warn("can not find geometry for id: " + _id);
    }
    Geometry.update(_id, {$set: {rotation: newRotation}});
  },
  'geometry.move': function(_id, newPosition) {
    let geometry = Geometry.findOne({_id: _id});
    if (!geometry) {
      console.warn("can not find geometry for id: " + _id);
    }
    Geometry.update(_id, {$set: {position: newPosition}});
  }
});
