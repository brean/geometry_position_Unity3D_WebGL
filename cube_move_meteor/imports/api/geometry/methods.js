import { Meteor } from 'meteor/meteor';
import { Geometry } from './geometry';

Meteor.methods({
  'geometry.add': function(type, position, rotation) {
    // todo: merge with responded/response_to?
    Geometry.insert({
      created: new Date(),
      type: type || "cube",
      position: position || [0, 0, 0],
      rotation: rotation || [0, 0, 0]
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
